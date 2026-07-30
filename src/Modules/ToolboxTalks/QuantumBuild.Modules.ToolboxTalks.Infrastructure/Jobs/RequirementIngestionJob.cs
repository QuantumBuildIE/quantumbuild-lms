using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Background job that fetches a regulatory document, extracts text via AI,
/// and persists draft RegulatoryRequirement records for SuperUser review.
/// </summary>
public class RequirementIngestionJob
{
    private const int MaxTokens = 8192;
    private readonly string _sonnetModel;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IPdfExtractionService _pdfExtractionService;
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly IAiUsageLogger _aiUsageLogger;
    private readonly ILogger<RequirementIngestionJob> _logger;

    public RequirementIngestionJob(
        IToolboxTalksDbContext dbContext,
        IPdfExtractionService pdfExtractionService,
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        IAiUsageLogger aiUsageLogger,
        ILogger<RequirementIngestionJob> logger,
        IOptions<AIProviderOptions> aiProviders)
    {
        _dbContext = dbContext;
        _pdfExtractionService = pdfExtractionService;
        _httpClient = httpClient;
        _settings = settings.Value;
        _aiUsageLogger = aiUsageLogger;
        _logger = logger;
        _sonnetModel = aiProviders.Value.Anthropic.Models.Sonnet;
    }

    [AutomaticRetry(Attempts = 1)]
    [Queue("content-generation")]
    public async Task ExecuteAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting requirement ingestion for document {DocumentId}", regulatoryDocumentId);

        RegulatoryDocument? document = null;

        try
        {
            // Load document with profiles
            document = await _dbContext.RegulatoryDocuments
                .Include(d => d.Profiles)
                    .ThenInclude(p => p.Sector)
                .FirstOrDefaultAsync(d => d.Id == regulatoryDocumentId, cancellationToken);

            if (document == null)
            {
                _logger.LogError("Document {DocumentId} not found", regulatoryDocumentId);
                return;
            }

            document.LastIngestionStatus = RegulatoryIngestionStatus.Ingesting;
            document.LastIngestionErrorCode = null;
            document.LastIngestionErrorMessage = null;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Defensive re-validation: the controller/service already reject invalid SourceUrls
            // before enqueueing, but this guards documents whose SourceUrl was written before
            // that validation existed (or written directly to the DB).
            if (!SourceUrlValidator.IsValid(document.SourceUrl, out var urlError))
            {
                await MarkFailedAsync(document, "invalid_uri", urlError!, cancellationToken);
                return;
            }

            // Step 1 — Fetch and extract text
            var fetchResult = await FetchDocumentTextAsync(document.SourceUrl!, cancellationToken);
            if (!fetchResult.Success || string.IsNullOrWhiteSpace(fetchResult.Text))
            {
                await MarkFailedAsync(
                    document,
                    fetchResult.ErrorCode ?? "fetch_failed",
                    fetchResult.ErrorMessage ?? "Failed to extract text from document.",
                    cancellationToken);
                return;
            }

            var extractedText = fetchResult.Text;
            _logger.LogInformation("Extracted {Length} characters from document {DocumentId}",
                extractedText.Length, regulatoryDocumentId);

            // Step 2 — Claude extraction, segmented by principle. A single whole-document call
            // truncates against the token cap on a real ~75-page/~150-requirement document, so
            // each of the document's principles gets its own call over the full document text,
            // scoped by prompt instruction to extract only that principle's requirements (mirrors
            // TranslationValidationJob's per-section call pattern). All segments are extracted and
            // validated in memory first — nothing is persisted until every segment has succeeded
            // (all-or-nothing: one failed segment fails the whole document, see MapSegmentFailures).
            //
            // Every principle is attempted regardless of an earlier segment's failure (run-all,
            // not fail-fast). Ingestion is rare, so the extra Claude calls on a failing document
            // are worth paying for a complete diagnosis of every principle that failed, rather
            // than only ever seeing the first one.
            var segmentOutcomes = new List<SegmentExtractionOutcome>();

            foreach (var principleNumber in PrincipleNumbers)
            {
                var outcome = await ExtractPrincipleSegmentAsync(extractedText, principleNumber, regulatoryDocumentId, cancellationToken);
                segmentOutcomes.Add(outcome);

                if (outcome.Failure != SegmentFailureReason.None)
                {
                    _logger.LogWarning(
                        "Principle {Principle} segment for document {DocumentId} failed ({Failure}); continuing to attempt remaining principles",
                        principleNumber, regulatoryDocumentId, outcome.Failure);
                    continue;
                }

                _logger.LogInformation(
                    "Principle {Principle} segment for document {DocumentId} extracted {Count} requirement(s)",
                    principleNumber, regulatoryDocumentId, outcome.Requirements!.Count);
            }

            var failedSegments = segmentOutcomes.Where(o => o.Failure != SegmentFailureReason.None).ToList();

            if (failedSegments.Count > 0)
            {
                var (errorCode, errorMessage) = MapSegmentFailures(failedSegments);
                await MarkFailedAsync(document, errorCode, errorMessage, cancellationToken);
                return;
            }

            // Assemble: concatenate segments in principle order and assign a single coherent
            // DisplayOrder across the whole document. Each segment's own model-supplied
            // displayOrder is only meaningful within that principle (see BuildExtractionPrompt)
            // and would collide across segments if trusted here, so it's always overwritten.
            var extractedRequirements = segmentOutcomes
                .SelectMany(o => o.Requirements ?? new List<ExtractedRequirement>())
                .ToList();

            for (var i = 0; i < extractedRequirements.Count; i++)
            {
                extractedRequirements[i].DisplayOrder = i + 1;
            }

            _logger.LogInformation(
                "Extracted {Count} requirements across {PrincipleCount} principles from document {DocumentId}",
                extractedRequirements.Count, PrincipleNumbers.Length, regulatoryDocumentId);

            // Step 3 — Persist as drafts for each matching profile
            var profiles = document.Profiles.Where(p => p.IsActive).ToList();
            if (profiles.Count == 0)
            {
                _logger.LogWarning(
                    "Document {DocumentId} has no active profiles — skipping persistence of {Count} extracted requirement(s)",
                    regulatoryDocumentId, extractedRequirements.Count);
                await MarkSkippedAsync(
                    document,
                    "no_active_profiles",
                    $"Extraction ran and returned {extractedRequirements.Count} candidate requirement(s), but this document has no active RegulatoryProfile to attach them to. Nothing was persisted.",
                    cancellationToken);
                return;
            }

            if (extractedRequirements.Count == 0)
            {
                _logger.LogWarning("Claude returned a well-formed but empty result for document {DocumentId}", regulatoryDocumentId);
                await MarkFailedAsync(
                    document,
                    "extraction_zero_requirements",
                    "Claude's response was valid JSON but contained zero requirements. No requirements were persisted.",
                    cancellationToken);
                return;
            }

            var totalCreated = 0;
            foreach (var profile in profiles)
            {
                var created = await PersistDraftRequirementsAsync(
                    profile, extractedRequirements, cancellationToken);
                totalCreated += created;
            }

            // Step 4 — Update document status
            await MarkSucceededAsync(document, cancellationToken);

            _logger.LogInformation(
                "Ingestion complete for document {DocumentId}: {Created} draft requirements created across {ProfileCount} profiles",
                regulatoryDocumentId, totalCreated, profiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ingestion job failed for document {DocumentId}: {Message}",
                regulatoryDocumentId, ex.Message);

            // Don't rethrow — Hangfire job should not fail noisily. But it must not swallow the
            // failure silently either: persist a Failed status so the frontend can surface it,
            // rather than leaving the document looking like ingestion never ran.
            if (document != null)
            {
                await MarkFailedAsync(document, "unknown", ex.Message, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Marks the document as Failed with a category + message, persisted immediately.
    /// </summary>
    private async Task MarkFailedAsync(
        RegulatoryDocument document,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        document.LastIngestionStatus = RegulatoryIngestionStatus.Failed;
        document.LastIngestionErrorCode = errorCode;
        document.LastIngestionErrorMessage = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogError(
            "Ingestion marked Failed for document {DocumentId}: [{ErrorCode}] {ErrorMessage}",
            document.Id, errorCode, document.LastIngestionErrorMessage);
    }

    /// <summary>
    /// Marks the document as Success and stamps LastIngestedAt, clearing any prior failure state.
    /// </summary>
    private async Task MarkSucceededAsync(RegulatoryDocument document, CancellationToken cancellationToken)
    {
        document.LastIngestedAt = DateTimeOffset.UtcNow;
        document.LastIngestionStatus = RegulatoryIngestionStatus.Success;
        document.LastIngestionErrorCode = null;
        document.LastIngestionErrorMessage = null;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Marks the document as Skipped — the run completed without error but had nothing to
    /// persist to (e.g. no active RegulatoryProfile). Distinct from both Success (which implies
    /// there was somewhere for a result to land) and Failed (which implies something went wrong).
    /// </summary>
    private async Task MarkSkippedAsync(
        RegulatoryDocument document,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        document.LastIngestedAt = DateTimeOffset.UtcNow;
        document.LastIngestionStatus = RegulatoryIngestionStatus.Skipped;
        document.LastIngestionErrorCode = errorCode;
        document.LastIngestionErrorMessage = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Ingestion marked Skipped for document {DocumentId}: [{ErrorCode}] {ErrorMessage}",
            document.Id, errorCode, document.LastIngestionErrorMessage);
    }

    /// <summary>
    /// Result of fetching + extracting text from a document's SourceUrl, with an error
    /// category ("invalid_uri", "fetch_failed", "parse_failed", "unknown") when it fails —
    /// letting ExecuteAsync persist an honest, distinguishable failure reason instead of the
    /// previous silent "return null" that left LastIngestedAt untouched forever.
    /// </summary>
    private sealed record DocumentFetchResult(bool Success, string? Text, string? ErrorCode, string? ErrorMessage)
    {
        public static DocumentFetchResult Ok(string text) => new(true, text, null, null);
        public static DocumentFetchResult Fail(string errorCode, string errorMessage) => new(false, null, errorCode, errorMessage);
    }

    private async Task<DocumentFetchResult> FetchDocumentTextAsync(
        string sourceUrl, CancellationToken cancellationToken)
    {
        if (sourceUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            // Use existing PDF extraction service — it already categorises its own failures.
            var result = await _pdfExtractionService.ExtractTextFromUrlAsync(sourceUrl, cancellationToken);
            if (!result.Success || string.IsNullOrWhiteSpace(result.Text))
            {
                _logger.LogError("PDF extraction failed for {Url}: [{Category}] {Error}",
                    sourceUrl, result.ErrorCategory, result.ErrorMessage);
                return DocumentFetchResult.Fail(
                    MapPdfErrorCategory(result.ErrorCategory),
                    result.ErrorMessage ?? "Failed to extract text from PDF.");
            }
            return DocumentFetchResult.Ok(result.Text);
        }

        try
        {
            // Fetch HTML and strip to plain text
            _logger.LogInformation("Fetching web page: {Url}", sourceUrl);
            var response = await _httpClient.GetAsync(sourceUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch URL {Url}: {Status}", sourceUrl, response.StatusCode);
                return DocumentFetchResult.Fail("fetch_failed", $"Failed to fetch URL. HTTP status: {response.StatusCode}");
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var text = StripHtmlToText(html);
            if (string.IsNullOrWhiteSpace(text))
            {
                return DocumentFetchResult.Fail("parse_failed", "Fetched page contained no extractable text.");
            }
            return DocumentFetchResult.Ok(text);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Not a fetch failure — genuine cancellation should propagate.
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Timed out fetching URL {Url}", sourceUrl);
            return DocumentFetchResult.Fail("fetch_failed", "Request to the source URL timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error fetching URL {Url}", sourceUrl);
            return DocumentFetchResult.Fail("fetch_failed", $"Failed to fetch URL: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Unsupported URI scheme fetching URL {Url}: {Message}", sourceUrl, ex.Message);
            return DocumentFetchResult.Fail("invalid_uri",
                "The source URL uses a scheme that cannot be fetched. Only http and https URLs are supported.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid URI fetching URL {Url}: {Message}", sourceUrl, ex.Message);
            return DocumentFetchResult.Fail("invalid_uri", $"The source URL could not be used: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Anything else that isn't cancellation: log honestly, categorise as unknown rather
            // than forcing a fit, and let ExecuteAsync's outer catch persist Failed regardless.
            _logger.LogError(ex, "Unexpected error fetching document from {Url}: {ExceptionType} — {Message}",
                sourceUrl, ex.GetType().Name, ex.Message);
            return DocumentFetchResult.Fail("unknown", $"Unexpected error while fetching document: {ex.Message}");
        }
    }

    private static string MapPdfErrorCategory(string? pdfErrorCategory) => pdfErrorCategory switch
    {
        PdfExtractionErrorCategory.UnsupportedScheme => "invalid_uri",
        PdfExtractionErrorCategory.NetworkError => "fetch_failed",
        PdfExtractionErrorCategory.Timeout => "fetch_failed",
        PdfExtractionErrorCategory.ParseFailure => "parse_failed",
        _ => "unknown"
    };

    /// <summary>
    /// The document's principles — one Claude call per principle, each scoped to the full
    /// document text but instructed to extract only that principle's requirements (see
    /// BuildExtractionPrompt). Principle-level segmentation only, not per-standard — the design
    /// trades 4x input cost for robustness against the whole-document truncation problem;
    /// ingestion is rare enough that this trade is intentional.
    /// </summary>
    private static readonly int[] PrincipleNumbers = { 1, 2, 3, 4 };

    /// <summary>
    /// Expected standard IDs per principle, used by the per-segment completeness check below: a
    /// segment can return valid, non-truncated JSON that is simply short (the model silently
    /// missed standards within the principle). THIS MAP IS HIQA-SPECIFIC — it encodes the
    /// structure of the single regulatory document currently seeded (RegulatoryRequirementSeedData:
    /// HIQA homecare). A second regulatory document with a different structure will need its own
    /// map (ideally derived from RegulatoryProfile config rather than hardcoded here) before it
    /// can be ingested through this job — do not assume this generalises as-is.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, string[]> HiqaExpectedStandardsByPrinciple =
        new Dictionary<int, string[]>
        {
            [1] = new[] { "1.1", "1.2", "1.3", "1.4" },
            [2] = new[] { "2.1", "2.2", "2.3", "2.4", "2.5" },
            [3] = new[] { "3.1", "3.2", "3.3" },
            [4] = new[] { "4.1", "4.2", "4.3", "4.4", "4.5" },
        };

    /// <summary>
    /// Why a principle segment produced nothing usable. None means <c>Requirements</c> is a
    /// valid, complete (per the expected-standards check) list; the other three mean the segment
    /// must be treated as a failure of the whole document — see the all-or-nothing persistence
    /// rule in ExecuteAsync and MapSegmentFailure.
    /// </summary>
    private enum SegmentFailureReason
    {
        None,
        Truncated,
        InvalidJson,
        Incomplete
    }

    private sealed record SegmentExtractionOutcome(
        int PrincipleNumber,
        List<ExtractedRequirement>? Requirements,
        SegmentFailureReason Failure,
        IReadOnlyList<string>? MissingStandards)
    {
        public static SegmentExtractionOutcome Success(int principleNumber, List<ExtractedRequirement> requirements) =>
            new(principleNumber, requirements, SegmentFailureReason.None, null);

        public static SegmentExtractionOutcome Truncated(int principleNumber) =>
            new(principleNumber, null, SegmentFailureReason.Truncated, null);

        public static SegmentExtractionOutcome InvalidJson(int principleNumber) =>
            new(principleNumber, null, SegmentFailureReason.InvalidJson, null);

        public static SegmentExtractionOutcome Incomplete(int principleNumber, List<ExtractedRequirement> requirements, IReadOnlyList<string> missingStandards) =>
            new(principleNumber, requirements, SegmentFailureReason.Incomplete, missingStandards);
    }

    /// <summary>
    /// Extracts one principle's requirements, retrying once (same shape as the prior
    /// whole-document retry) if the first attempt is truncated, unparseable, or — new in the
    /// segmented design — parseable but missing expected standards for this principle.
    /// </summary>
    private async Task<SegmentExtractionOutcome> ExtractPrincipleSegmentAsync(
        string documentText, int principleNumber, Guid documentId, CancellationToken cancellationToken)
    {
        var prompt = BuildExtractionPrompt(documentText, principleNumber);

        // First attempt
        var (responseText, stopReason) = await CallClaudeAsync(prompt, documentId, cancellationToken);
        var requirements = TryParseRequirements(responseText);
        var truncated = stopReason == "max_tokens";
        List<string>? missingStandards = null;

        if (requirements != null && !truncated)
        {
            missingStandards = FindMissingStandards(principleNumber, requirements);
            if (missingStandards.Count == 0)
                return SegmentExtractionOutcome.Success(principleNumber, requirements);

            _logger.LogWarning(
                "Principle {Principle} segment for document {DocumentId} is missing expected standards [{Missing}]; retrying",
                principleNumber, documentId, string.Join(", ", missingStandards));
        }
        else if (truncated)
        {
            _logger.LogWarning(
                "Principle {Principle} extraction attempt for document {DocumentId} was truncated (stop_reason=max_tokens); retrying",
                principleNumber, documentId);
        }
        else
        {
            _logger.LogWarning(
                "Principle {Principle} extraction attempt for document {DocumentId} returned invalid JSON, retrying with stricter prompt",
                principleNumber, documentId);
        }

        // Retry — same segment prompt, stricter instruction appended. When the first attempt was
        // incomplete rather than truncated/unparseable, the retry names the missing standards.
        var stricterPrompt = BuildStricterPrompt(prompt, missingStandards);

        var (retryResponseText, retryStopReason) = await CallClaudeAsync(stricterPrompt, documentId, cancellationToken);
        var retryRequirements = TryParseRequirements(retryResponseText);
        var retryTruncated = retryStopReason == "max_tokens";

        if (retryRequirements != null && !retryTruncated)
        {
            var retryMissing = FindMissingStandards(principleNumber, retryRequirements);
            if (retryMissing.Count == 0)
                return SegmentExtractionOutcome.Success(principleNumber, retryRequirements);

            _logger.LogError(
                "Principle {Principle} segment retry for document {DocumentId} is still missing expected standards [{Missing}]",
                principleNumber, documentId, string.Join(", ", retryMissing));
            return SegmentExtractionOutcome.Incomplete(principleNumber, retryRequirements, retryMissing);
        }

        if (retryTruncated)
        {
            _logger.LogError(
                "Principle {Principle} extraction retry for document {DocumentId} was also truncated (stop_reason=max_tokens)",
                principleNumber, documentId);
            return SegmentExtractionOutcome.Truncated(principleNumber);
        }

        _logger.LogError(
            "Failed to parse Principle {Principle} requirements from Claude response for document {DocumentId} after retry",
            principleNumber, documentId);
        return SegmentExtractionOutcome.InvalidJson(principleNumber);
    }

    private static string BuildStricterPrompt(string basePrompt, List<string>? missingStandards)
    {
        if (missingStandards is { Count: > 0 })
        {
            return basePrompt +
                "\n\nIMPORTANT: Your previous response did not include a requirement for every standard in this principle. " +
                $"It was missing: {string.Join(", ", missingStandards)}. You MUST extract at least one requirement for EVERY standard under this principle. " +
                "Respond ONLY with a valid JSON array — no preamble, no markdown, no explanation.";
        }

        return basePrompt +
            "\n\nIMPORTANT: Your previous response was not valid JSON. You MUST respond with ONLY a JSON array. " +
            "No text before or after. No markdown code fences. Just the raw JSON array starting with [ and ending with ].";
    }

    /// <summary>
    /// Returns the expected standard IDs for <paramref name="principleNumber"/> (per
    /// HiqaExpectedStandardsByPrinciple) that don't appear in any extracted requirement's Section
    /// field. Matching is a literal substring search with digit-boundary guards (e.g. "1.1" won't
    /// match inside "11.1") since Section is free text like "Standard 1.1" — there is no
    /// structured standard-ID field on the extraction DTO. An unmapped principle number (no entry
    /// in the map) always returns no missing standards: the check is skipped, not failed, for
    /// documents whose structure hasn't been mapped yet.
    /// </summary>
    private static List<string> FindMissingStandards(int principleNumber, List<ExtractedRequirement> requirements)
    {
        if (!HiqaExpectedStandardsByPrinciple.TryGetValue(principleNumber, out var expectedStandards))
            return new List<string>();

        var sections = requirements
            .Where(r => !string.IsNullOrWhiteSpace(r.Section))
            .Select(r => r.Section!)
            .ToList();

        var missing = new List<string>();
        foreach (var standardId in expectedStandards)
        {
            var pattern = $@"(?<!\d){System.Text.RegularExpressions.Regex.Escape(standardId)}(?!\d)";
            var found = sections.Any(s => System.Text.RegularExpressions.Regex.IsMatch(s, pattern));
            if (!found)
                missing.Add(standardId);
        }

        return missing;
    }

    /// <summary>
    /// Priority order used to pick the single LastIngestionErrorCode when failed segments have
    /// different failure reasons, ranked least to most diagnosable on its own: InvalidJson means
    /// no parseable output was returned at all; Truncated means some output arrived but hit the
    /// hard token limit; Incomplete means the model understood the task and returned valid,
    /// parseable JSON that was merely missing a standard. The highest-ranked reason present wins
    /// the single-valued code. This ranking only affects which code is stored — the full
    /// per-principle detail for every failure always lands in LastIngestionErrorMessage regardless
    /// of which code wins here.
    /// </summary>
    private static readonly SegmentFailureReason[] ReasonPriority =
    {
        SegmentFailureReason.InvalidJson,
        SegmentFailureReason.Truncated,
        SegmentFailureReason.Incomplete
    };

    private static string ErrorCodeFor(SegmentFailureReason reason) => reason switch
    {
        SegmentFailureReason.Truncated => "extraction_truncated",
        SegmentFailureReason.InvalidJson => "extraction_invalid_json",
        SegmentFailureReason.Incomplete => "extraction_incomplete",
        _ => "unknown"
    };

    /// <summary>
    /// Describes a single failed segment's reason, without the "No requirements were persisted"
    /// closing sentence — that belongs once, at the end of the aggregated message built by
    /// MapSegmentFailures, not repeated per principle.
    /// </summary>
    private static string DescribeSegmentReason(SegmentExtractionOutcome outcome) => outcome.Failure switch
    {
        SegmentFailureReason.Truncated =>
            $"response was truncated (stop_reason=max_tokens) on both the initial attempt and the retry - the {MaxTokens}-token output limit was too small for this segment",

        SegmentFailureReason.InvalidJson =>
            "response could not be parsed as valid JSON on either the initial attempt or the retry",

        SegmentFailureReason.Incomplete =>
            $"response was missing expected standards ({string.Join(", ", outcome.MissingStandards ?? new List<string>())}) after a retry - the segment appears incomplete rather than truncated",

        _ => "failed for an unrecognised reason"
    };

    /// <summary>
    /// Builds the aggregated failure code/message across every principle segment that failed
    /// (run-all-collect-failures: every principle is attempted before this is called, see
    /// ExecuteAsync). Two shapes:
    ///
    /// - Common root cause: every failed segment failed for the identical reason (e.g. the whole
    ///   document's text is malformed and every principle truncates identically). Reads as one
    ///   failure story naming all affected principles, not N unrelated failures.
    /// - Mixed reasons: failed segments enumerated individually, each naming its own principle
    ///   and reason, so no distinct failure is lost to the single-valued error code.
    /// </summary>
    private static (string ErrorCode, string ErrorMessage) MapSegmentFailures(
        IReadOnlyList<SegmentExtractionOutcome> failedSegments)
    {
        var distinctReasons = failedSegments.Select(f => f.Failure).Distinct().ToList();
        var primaryReason = distinctReasons.Count == 1
            ? distinctReasons[0]
            : ReasonPriority.First(distinctReasons.Contains);
        var errorCode = ErrorCodeFor(primaryReason);

        if (distinctReasons.Count == 1 && failedSegments.Count > 1)
        {
            var principles = string.Join(", ", failedSegments.Select(f => f.PrincipleNumber));
            var sharedReason = DescribeSegmentReason(failedSegments[0]);
            return (errorCode,
                $"All {failedSegments.Count} of {PrincipleNumbers.Length} principles ({principles}) failed for the same reason: {sharedReason}. No requirements were persisted for any principle.");
        }

        var perPrincipleDetails = string.Join("; ", failedSegments.Select(f =>
            $"Principle {f.PrincipleNumber}: {DescribeSegmentReason(f)}"));

        return (errorCode,
            $"{failedSegments.Count} of {PrincipleNumbers.Length} principles failed extraction. {perPrincipleDetails}. No requirements were persisted for any principle.");
    }

    private static string BuildExtractionPrompt(string documentText, int principleNumber)
    {
        return $@"You are a regulatory compliance expert. Analyse the following regulatory document and extract only the requirements under Principle {principleNumber} that relate to staff training, competency, or compliance obligations.

The document below contains multiple principles. You are extracting ONLY Principle {principleNumber}'s requirements — do not extract requirements belonging to any other principle, and do not skip any standard within Principle {principleNumber}.

For each requirement, extract:
- title: A concise title (max 200 chars) for the training/competency requirement
- description: A detailed description (max 2000 chars) of what the requirement entails
- section: The section or article reference from the document (e.g. ""Standard 2.3"", ""Article 4"", ""§7""). Use the standard numbering from the source document. If not explicitly stated, use the document section heading or ""General"". This field is MANDATORY — never return null or omit it
- sectionLabel: A short descriptive label for the section (e.g. ""Incident Reporting"", ""Staff Training"", ""MAR Management""). Derive from context if not explicit. This field is MANDATORY — never return null or omit it
- principle: A short category label grouping this requirement (e.g. ""P2"", ""Staff Competency"", ""Food Safety Management""). If not explicitly stated in the document, infer from the requirement's subject matter. This field is MANDATORY — never return null or omit it
- principleLabel: The full description of the principle category. MUST use one of the exact canonical labels below when applicable, or derive a descriptive label from the requirement's subject matter
- priority: ""high"" for safety-critical requirements, ""med"" for standard compliance, ""low"" for best-practice/advisory
- displayOrder: Sequential numbering within THIS principle's requirements, starting from 1 (final numbering across the whole document is assigned separately once all principles are extracted)

CANONICAL PRINCIPLE LABELS (use these exact strings — do not paraphrase or reword):
- P2 — ""Safety & Wellbeing""
- P3 — ""Responsiveness""
- P4 — ""Accountability""

If the document uses different wording (e.g. ""Safety and Wellbeing""), map it to the canonical label above.
If the principle does not match any of these, set principleLabel to the document's exact text.

IMPORTANT RULES:
- Extract ONLY requirements belonging to Principle {principleNumber} that relate to staff training, competency, skills, or compliance obligations
- Do NOT include general policy statements, organisational structure requirements, or non-training items
- Each requirement should be actionable as a training topic
- The fields section, sectionLabel, principle, and principleLabel are ALL MANDATORY — never return null or omit them. If the document does not explicitly state them, infer reasonable values from the requirement's context and subject matter
- Respond ONLY with a valid JSON array — no preamble, no markdown, no explanation

DOCUMENT TEXT:
{documentText}";
    }

    private async Task<(string ContentText, string? StopReason)> CallClaudeAsync(
        string prompt, Guid documentId, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = _sonnetModel,
            max_tokens = MaxTokens,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.Claude.BaseUrl}/messages");
        request.Headers.Add("x-api-key", _settings.Claude.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Claude API error: {Status} — {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Claude API error: {response.StatusCode}");
        }

        var parsed = AnthropicResponseParser.Parse(responseBody);
        var stopReason = ExtractStopReason(responseBody);

        await _aiUsageLogger.LogAsync(
            Guid.Empty,
            AiOperationCategory.RequirementIngestion,
            parsed.Model,
            parsed.InputTokens,
            parsed.OutputTokens,
            isSystemCall: true,
            userId: null,
            referenceEntityId: documentId,
            cancellationToken);

        return (parsed.ContentText, stopReason);
    }

    // Mirrors AiSlideshowGenerationService.ExtractStopReason — AnthropicResponseParser doesn't
    // carry stop_reason, so it's read directly off the raw response body here too.
    private static string? ExtractStopReason(string responseBody)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);
        return jsonDoc.RootElement.TryGetProperty("stop_reason", out var stopEl)
            ? stopEl.GetString()
            : null;
    }

    /// <summary>
    /// Returns null only when the response could not be parsed as JSON at all (garbage text,
    /// markdown-wrapped non-JSON, or truncated mid-object). A syntactically valid but empty
    /// array ("[]") returns an empty list, not null — callers must not conflate "unparseable"
    /// with "parsed cleanly to nothing", since only the former is a retry-worthy failure.
    /// </summary>
    private List<ExtractedRequirement>? TryParseRequirements(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
            return null;

        try
        {
            // Strip markdown code fences if present
            var json = responseText.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewline = json.IndexOf('\n');
                if (firstNewline > 0) json = json[(firstNewline + 1)..];
                if (json.EndsWith("```")) json = json[..^3];
                json = json.Trim();
            }

            return JsonSerializer.Deserialize<List<ExtractedRequirement>>(json, CamelCaseOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Claude response as JSON: {Preview}",
                responseText.Length > 200 ? responseText[..200] : responseText);
            return null;
        }
    }

    private async Task<int> PersistDraftRequirementsAsync(
        RegulatoryProfile profile,
        List<ExtractedRequirement> extractedRequirements,
        CancellationToken cancellationToken)
    {
        // Load existing titles for duplicate check (include soft-deleted)
        var existingTitles = await _dbContext.RegulatoryRequirements
            .IgnoreQueryFilters()
            .Where(r => r.RegulatoryProfileId == profile.Id)
            .Select(r => r.Title.ToLower())
            .ToListAsync(cancellationToken);

        var existingTitleSet = new HashSet<string>(existingTitles);
        var created = 0;

        foreach (var extracted in extractedRequirements)
        {
            if (string.IsNullOrWhiteSpace(extracted.Title))
            {
                _logger.LogWarning("Skipping requirement with empty title");
                continue;
            }

            if (existingTitleSet.Contains(extracted.Title.ToLower()))
            {
                _logger.LogDebug("Skipping duplicate requirement: {Title}", extracted.Title);
                continue;
            }

            var requirement = new RegulatoryRequirement
            {
                RegulatoryProfileId = profile.Id,
                Title = extracted.Title.Length > 200 ? extracted.Title[..200] : extracted.Title,
                Description = string.IsNullOrWhiteSpace(extracted.Description)
                    ? extracted.Title
                    : extracted.Description.Length > 2000
                        ? extracted.Description[..2000]
                        : extracted.Description,
                Section = extracted.Section?.Length > 20 ? extracted.Section[..20] : extracted.Section,
                SectionLabel = extracted.SectionLabel?.Length > 200 ? extracted.SectionLabel[..200] : extracted.SectionLabel,
                Principle = extracted.Principle?.Length > 20 ? extracted.Principle[..20] : extracted.Principle,
                PrincipleLabel = extracted.PrincipleLabel?.Length > 200 ? extracted.PrincipleLabel[..200] : extracted.PrincipleLabel,
                Priority = ValidatePriority(extracted.Priority),
                DisplayOrder = extracted.DisplayOrder > 0 ? extracted.DisplayOrder : created + 1,
                IngestionSource = RequirementIngestionSource.Automated,
                IngestionStatus = RequirementIngestionStatus.Draft,
                IsActive = true,
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow,
            };

            _dbContext.RegulatoryRequirements.Add(requirement);
            existingTitleSet.Add(extracted.Title.ToLower());
            created++;
        }

        if (created > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Created {Count} draft requirements for profile {ProfileId} (sector: {SectorKey})",
                created, profile.Id, profile.SectorKey);
        }

        return created;
    }

    private static string ValidatePriority(string? priority)
    {
        return priority?.ToLower() switch
        {
            "high" => "high",
            "low" => "low",
            _ => "med"
        };
    }

    private static string StripHtmlToText(string html)
    {
        // Remove script and style blocks
        var text = System.Text.RegularExpressions.Regex.Replace(
            html, @"<(script|style)[^>]*>[\s\S]*?</\1>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Replace block tags with newlines
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<(br|p|div|h[1-6]|li|tr)[^>]*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Strip remaining tags
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
        // Decode HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);
        // Collapse whitespace
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    /// <summary>
    /// Intermediate DTO for deserialization of Claude's JSON response
    /// </summary>
    private class ExtractedRequirement
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Section { get; set; }
        public string? SectionLabel { get; set; }
        public string? Principle { get; set; }
        public string? PrincipleLabel { get; set; }
        public string? Priority { get; set; }
        public int DisplayOrder { get; set; }
    }
}
