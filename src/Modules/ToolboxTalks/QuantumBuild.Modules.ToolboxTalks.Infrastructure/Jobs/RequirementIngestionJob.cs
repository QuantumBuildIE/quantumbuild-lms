using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Background job that fetches a regulatory document, transcribes it via AI against the
/// document's structure map, and persists draft RegulatoryRequirement records for review.
///
/// MAP-DRIVEN VERBATIM TRANSCRIPTION (docs/faithful-extraction-build-recon.md). This job no
/// longer asks the model to judge what requirements exist. The document's structure map
/// (IRegulatoryStructureMapProvider) is the authoritative list of every feature the document
/// defines — identifier, block (person-experience vs provider), and which standard it belongs
/// to. Extraction's only job is to locate each declared feature's exact printed text in the
/// freshly-fetched document and transcribe it verbatim; the model cannot add features the map
/// doesn't declare or drop ones it does, because persistence is driven by walking the map's own
/// feature list, not by whatever the model happens to return (see PersistDraftRequirementsAsync —
/// any extra/unrecognised items in a model response are simply never looked up and so never
/// persisted, regardless of whether the model obeyed the "don't invent features" instruction).
///
/// A document with no registered structure map fails loudly ("no_structure_map") rather than
/// silently no-op'ing — there is nothing to drive extraction or completeness-check without one.
/// A document whose map is still Draft (unverified) is NOT blocked — that would stop all testing
/// before the first human review — but every requirement persisted from it carries
/// IsProvisional = true, a visible marker that the structure it transcribed against is not yet
/// human-confirmed.
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
    private readonly IRegulatoryStructureMapProvider _structureMapProvider;
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly IAiUsageLogger _aiUsageLogger;
    private readonly ILogger<RequirementIngestionJob> _logger;

    public RequirementIngestionJob(
        IToolboxTalksDbContext dbContext,
        IPdfExtractionService pdfExtractionService,
        IRegulatoryStructureMapProvider structureMapProvider,
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        IAiUsageLogger aiUsageLogger,
        ILogger<RequirementIngestionJob> logger,
        IOptions<AIProviderOptions> aiProviders)
    {
        _dbContext = dbContext;
        _pdfExtractionService = pdfExtractionService;
        _structureMapProvider = structureMapProvider;
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
                await MarkFailedAsync(document.Id, "invalid_uri", urlError!, cancellationToken);
                return;
            }

            // Step 1 — Resolve the document's structure map. Extraction is bounded transcription
            // against this map's declared features, not free judgment — a document with no map
            // has nothing to drive extraction or completeness-checking against, and must fail
            // loudly rather than silently no-op the way the old per-principle expected-standards
            // fallback used to for an unmapped principle number.
            var (mapFound, structureMap) = await _structureMapProvider.TryGetStructureMapAsync(
                document.Id, cancellationToken);

            if (!mapFound || structureMap == null)
            {
                await MarkFailedAsync(
                    document.Id,
                    RegulatoryStructureMapNotFoundException.ErrorCode,
                    $"No structure map is registered for document '{document.Title}' ({document.Id}). " +
                    "Extraction cannot proceed without a per-document structure map declaring its features.",
                    cancellationToken);
                return;
            }

            // A Draft (unverified) map does not block extraction — every requirement persisted
            // from it is simply flagged IsProvisional so downstream review knows the structure
            // it was transcribed against has not yet been human-confirmed.
            var isProvisional = !structureMap.IsVerified;
            if (isProvisional)
            {
                _logger.LogWarning(
                    "Structure map for document {DocumentId} is still Draft (unverified) — extraction will proceed, but every persisted requirement will be flagged IsProvisional",
                    regulatoryDocumentId);
            }

            // Step 2 — Fetch and extract text
            var fetchResult = await FetchDocumentTextAsync(document.SourceUrl!, cancellationToken);
            if (!fetchResult.Success || string.IsNullOrWhiteSpace(fetchResult.Text))
            {
                await MarkFailedAsync(
                    document.Id,
                    fetchResult.ErrorCode ?? "fetch_failed",
                    fetchResult.ErrorMessage ?? "Failed to extract text from document.",
                    cancellationToken);
                return;
            }

            var extractedText = fetchResult.Text;
            _logger.LogInformation("Extracted {Length} characters from document {DocumentId}",
                extractedText.Length, regulatoryDocumentId);

            // Step 3 — Claude transcription, segmented per standard. Segmentation granularity is
            // per-standard (not per-principle, as it was before the map-driven rework) — verbatim
            // text is materially longer than the free-judged title/description pairs the old
            // prompt asked for, so keeping each call's declared-feature set to a single standard's
            // person+provider blocks (at most 13 features for HIQA, ~500 chars of verbatim text
            // each) keeps every call's output well within the token cap, rather than risking
            // truncation by asking for a whole principle's worth of verbatim text in one call.
            // This also generalises to any mapped document (17 calls for HIQA today, however many
            // standards a future document's map declares tomorrow) rather than assuming a fixed
            // principle count. All segments are extracted and validated in memory first — nothing
            // is persisted until every segment has succeeded (all-or-nothing: one failed segment
            // fails the whole document, see MapSegmentFailures).
            //
            // Every standard is attempted regardless of an earlier segment's failure (run-all,
            // not fail-fast). Ingestion is rare, so the extra Claude calls on a failing document
            // are worth paying for a complete diagnosis of every standard that failed, rather
            // than only ever seeing the first one.
            var segmentOutcomes = new List<SegmentExtractionOutcome>();
            var totalStandards = structureMap.AllStandards.Count();

            foreach (var principle in structureMap.Principles)
            {
                foreach (var standard in principle.Standards)
                {
                    var outcome = await ExtractStandardSegmentAsync(
                        extractedText, principle.Number, standard, regulatoryDocumentId, cancellationToken);
                    segmentOutcomes.Add(outcome);

                    if (outcome.Failure != SegmentFailureReason.None)
                    {
                        _logger.LogWarning(
                            "Standard {StandardId} (Principle {Principle}) segment for document {DocumentId} failed ({Failure}); continuing to attempt remaining standards",
                            standard.Id, principle.Number, regulatoryDocumentId, outcome.Failure);
                        continue;
                    }

                    _logger.LogInformation(
                        "Standard {StandardId} (Principle {Principle}) segment for document {DocumentId} transcribed {Count} feature(s)",
                        standard.Id, principle.Number, regulatoryDocumentId, outcome.Features!.Count);
                }
            }

            var failedSegments = segmentOutcomes.Where(o => o.Failure != SegmentFailureReason.None).ToList();

            if (failedSegments.Count > 0)
            {
                var (errorCode, errorMessage) = MapSegmentFailures(failedSegments, totalStandards);
                await MarkFailedAsync(document.Id, errorCode, errorMessage, cancellationToken);
                return;
            }

            // Assemble: walk the map's own declared order (principle -> standard -> feature,
            // person-then-provider per the map's authored order) and pair each declared feature
            // with the verbatim text its segment's extraction produced for it. DisplayOrder is
            // therefore stable across runs — it derives from the fixed map, not from per-call
            // model output, so there is no restart-per-segment collision risk.
            var transcribed = new List<TranscribedFeature>();
            foreach (var outcome in segmentOutcomes)
            {
                var byKey = new Dictionary<(string Identifier, RequirementBlock Block), string>();
                foreach (var extractedFeature in outcome.Features!)
                {
                    var block = ParseBlock(extractedFeature.Block);
                    if (block == null ||
                        string.IsNullOrWhiteSpace(extractedFeature.Identifier) ||
                        string.IsNullOrWhiteSpace(extractedFeature.VerbatimText))
                        continue;

                    // First occurrence wins if the model duplicates a feature — deterministic,
                    // and avoids a crash on a duplicate-key insert.
                    byKey.TryAdd((extractedFeature.Identifier.Trim(), block.Value), extractedFeature.VerbatimText);
                }

                foreach (var mapFeature in outcome.Standard.Features)
                {
                    // Guaranteed present — the per-segment completeness check above (FindMissingFeatures)
                    // already confirmed every map-declared feature has a matching, non-empty entry.
                    var verbatimText = byKey[(mapFeature.Identifier, mapFeature.Block)];
                    transcribed.Add(new TranscribedFeature
                    {
                        PrincipleNumber = outcome.PrincipleNumber,
                        StandardId = outcome.Standard.Id,
                        MapFeature = mapFeature,
                        VerbatimText = verbatimText
                    });
                }
            }

            for (var i = 0; i < transcribed.Count; i++)
            {
                transcribed[i].DisplayOrder = i + 1;
            }

            _logger.LogInformation(
                "Transcribed {Count} features across {StandardCount} standards from document {DocumentId}",
                transcribed.Count, totalStandards, regulatoryDocumentId);

            // Step 3.5 — Feature-level completeness + attribution check against the FULL map
            // (docs/faithful-extraction-build-recon.md, sub-chunk 3). The per-segment check above
            // (FindMissingFeatures) already guarantees each standard's own response is complete
            // before assembly runs, and map-driven assembly structurally cannot introduce
            // unexpected or misattributed candidates today — but MapCoverageVerifier makes that an
            // explicit, checked property of the whole document rather than an implicit guarantee of
            // how assembly happens to be coded. Runs unconditionally, regardless of isProvisional —
            // see MapCoverageVerifier's remarks for why Draft vs Verified never enters this check.
            var candidates = transcribed
                .Select(t => new CandidateFeature(t.MapFeature.Identifier, t.MapFeature.Block, t.StandardId, t.PrincipleNumber))
                .ToList();
            var coverage = MapCoverageVerifier.Verify(structureMap, candidates);

            if (!coverage.IsComplete)
            {
                var (coverageErrorCode, coverageErrorMessage) = DescribeCoverageFailure(coverage);
                await MarkFailedAsync(document.Id, coverageErrorCode, coverageErrorMessage, cancellationToken);
                return;
            }

            // Step 4 — Persist as drafts for each matching profile
            var profiles = document.Profiles.Where(p => p.IsActive).ToList();
            if (profiles.Count == 0)
            {
                _logger.LogWarning(
                    "Document {DocumentId} has no active profiles — skipping persistence of {Count} transcribed feature(s)",
                    regulatoryDocumentId, transcribed.Count);
                await MarkSkippedAsync(
                    document,
                    "no_active_profiles",
                    $"Extraction ran and transcribed {transcribed.Count} feature(s), but this document has no active RegulatoryProfile to attach them to. Nothing was persisted.",
                    cancellationToken);
                return;
            }

            if (transcribed.Count == 0)
            {
                _logger.LogWarning("Structure map for document {DocumentId} declares zero features", regulatoryDocumentId);
                await MarkFailedAsync(
                    document.Id,
                    "extraction_zero_requirements",
                    "The document's structure map declares zero features. No requirements were persisted.",
                    cancellationToken);
                return;
            }

            var totalCreated = 0;
            foreach (var profile in profiles)
            {
                var created = await PersistDraftRequirementsAsync(
                    profile, transcribed, isProvisional, cancellationToken);
                totalCreated += created;
            }

            // Step 5 — Update document status
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

            // Always attempt to record Failed by id rather than trusting the local `document`
            // reference — MarkFailedAsync re-queries internally, so this covers both the common
            // case (document loaded, then something later failed) and the narrow pre-load gap
            // (the exception happened during the very first load above, so `document` is still
            // null) with a single call. See docs/ingestion-terminal-state-recon.md (a).
            await MarkFailedAsync(regulatoryDocumentId, "unknown", ex.Message, cancellationToken);

            // Rethrow so Hangfire sees the failure and [AutomaticRetry(Attempts = 1)] can
            // actually fire — previously this catch swallowed every exception, so Hangfire
            // recorded every run as Succeeded regardless of outcome, making the retry attribute
            // dead code. Safe specifically because extraction is all-or-nothing:
            // PersistDraftRequirementsAsync (below) checks existing (including soft-deleted)
            // requirement (FeatureIdentifier, Block) pairs per profile before inserting, so a
            // retry that redoes the fetch+extraction from scratch cannot double-persist a feature
            // a prior attempt already created. Every *expected* non-fatal outcome (invalid_uri,
            // no_structure_map, fetch/parse failures, the extraction_* segment failures, the
            // no_active_profiles skip, zero-requirements) returns from inside the try block above
            // via its own Mark*Async call — none of those throw, so none of them reach this catch
            // or get rethrown here. Only genuine unexpected exceptions do.
            throw;
        }
    }

    /// <summary>
    /// Marks the document as Failed with a category + message, persisted immediately.
    /// Re-queries the document by id (mirrors TranslationValidationJob.UpdateRunStatusAsync)
    /// rather than accepting an already-loaded reference, so this single method covers both the
    /// normal case and the pre-document-load exception window where no tracked instance exists
    /// yet. The save itself is nested in its own try/catch: a failure to persist the Failed
    /// status must not itself escape uncaught (e.g. a DB blip at the exact moment of this write)
    /// — if it does fail, the document remains on Ingesting until either the caller's rethrow
    /// lets Hangfire's automatic retry re-run the job cleanly, or the staleness sweep
    /// (StaleIngestionSweepJob) eventually force-terminates it.
    /// </summary>
    private async Task MarkFailedAsync(
        Guid documentId,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        var truncatedMessage = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage;

        try
        {
            var document = await _dbContext.RegulatoryDocuments
                .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

            if (document == null)
            {
                _logger.LogError(
                    "Cannot mark ingestion Failed for document {DocumentId} — document no longer exists",
                    documentId);
                return;
            }

            document.LastIngestionStatus = RegulatoryIngestionStatus.Failed;
            document.LastIngestionErrorCode = errorCode;
            document.LastIngestionErrorMessage = truncatedMessage;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                "Ingestion marked Failed for document {DocumentId}: [{ErrorCode}] {ErrorMessage}",
                documentId, errorCode, truncatedMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to persist Failed status for document {DocumentId} — it may remain on " +
                "Ingesting until the next automatic retry or the staleness sweep clears it",
                documentId);
        }
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
    /// Why a standard segment produced nothing usable. None means <c>Features</c> is a valid,
    /// complete (every feature the map declares for this standard is present) list; the other
    /// three mean the segment must be treated as a failure of the whole document — see the
    /// all-or-nothing persistence rule in ExecuteAsync and MapSegmentFailures.
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
        StructureStandard Standard,
        List<ExtractedFeature>? Features,
        SegmentFailureReason Failure,
        IReadOnlyList<string>? MissingFeatures)
    {
        public static SegmentExtractionOutcome Success(int principleNumber, StructureStandard standard, List<ExtractedFeature> features) =>
            new(principleNumber, standard, features, SegmentFailureReason.None, null);

        public static SegmentExtractionOutcome Truncated(int principleNumber, StructureStandard standard) =>
            new(principleNumber, standard, null, SegmentFailureReason.Truncated, null);

        public static SegmentExtractionOutcome InvalidJson(int principleNumber, StructureStandard standard) =>
            new(principleNumber, standard, null, SegmentFailureReason.InvalidJson, null);

        public static SegmentExtractionOutcome Incomplete(int principleNumber, StructureStandard standard, List<ExtractedFeature> features, IReadOnlyList<string> missingFeatures) =>
            new(principleNumber, standard, features, SegmentFailureReason.Incomplete, missingFeatures);
    }

    /// <summary>
    /// Transcribes one standard's declared features (both person and provider blocks together),
    /// retrying once (same shape as the prior per-principle retry) if the first attempt is
    /// truncated, unparseable, or — the map-driven completeness check — parseable but missing a
    /// verbatim entry for one or more of the standard's declared features.
    /// </summary>
    private async Task<SegmentExtractionOutcome> ExtractStandardSegmentAsync(
        string documentText, int principleNumber, StructureStandard standard, Guid documentId, CancellationToken cancellationToken)
    {
        var prompt = BuildExtractionPrompt(documentText, principleNumber, standard);

        // First attempt
        var (responseText, stopReason) = await CallClaudeAsync(prompt, documentId, cancellationToken);
        var features = TryParseFeatures(responseText);
        var truncated = stopReason == "max_tokens";
        List<string>? missingFeatures = null;

        if (features != null && !truncated)
        {
            missingFeatures = FindMissingFeatures(standard, features);
            if (missingFeatures.Count == 0)
                return SegmentExtractionOutcome.Success(principleNumber, standard, features);

            _logger.LogWarning(
                "Standard {StandardId} segment for document {DocumentId} is missing declared features [{Missing}]; retrying",
                standard.Id, documentId, string.Join(", ", missingFeatures));
        }
        else if (truncated)
        {
            _logger.LogWarning(
                "Standard {StandardId} extraction attempt for document {DocumentId} was truncated (stop_reason=max_tokens); retrying",
                standard.Id, documentId);
        }
        else
        {
            _logger.LogWarning(
                "Standard {StandardId} extraction attempt for document {DocumentId} returned invalid JSON, retrying with stricter prompt",
                standard.Id, documentId);
        }

        // Retry — same segment prompt, stricter instruction appended. When the first attempt was
        // incomplete rather than truncated/unparseable, the retry names the missing features.
        var stricterPrompt = BuildStricterPrompt(prompt, missingFeatures);

        var (retryResponseText, retryStopReason) = await CallClaudeAsync(stricterPrompt, documentId, cancellationToken);
        var retryFeatures = TryParseFeatures(retryResponseText);
        var retryTruncated = retryStopReason == "max_tokens";

        if (retryFeatures != null && !retryTruncated)
        {
            var retryMissing = FindMissingFeatures(standard, retryFeatures);
            if (retryMissing.Count == 0)
                return SegmentExtractionOutcome.Success(principleNumber, standard, retryFeatures);

            _logger.LogError(
                "Standard {StandardId} segment retry for document {DocumentId} is still missing declared features [{Missing}]",
                standard.Id, documentId, string.Join(", ", retryMissing));
            return SegmentExtractionOutcome.Incomplete(principleNumber, standard, retryFeatures, retryMissing);
        }

        if (retryTruncated)
        {
            _logger.LogError(
                "Standard {StandardId} extraction retry for document {DocumentId} was also truncated (stop_reason=max_tokens)",
                standard.Id, documentId);
            return SegmentExtractionOutcome.Truncated(principleNumber, standard);
        }

        _logger.LogError(
            "Failed to parse Standard {StandardId} features from Claude response for document {DocumentId} after retry",
            standard.Id, documentId);
        return SegmentExtractionOutcome.InvalidJson(principleNumber, standard);
    }

    private static string BuildStricterPrompt(string basePrompt, List<string>? missingFeatures)
    {
        if (missingFeatures is { Count: > 0 })
        {
            return basePrompt +
                "\n\nIMPORTANT: Your previous response did not include every declared feature for this standard. " +
                $"It was missing: {string.Join(", ", missingFeatures)}. You MUST transcribe EVERY feature listed above, exactly once each. " +
                "Respond ONLY with a valid JSON array — no preamble, no markdown, no explanation.";
        }

        return basePrompt +
            "\n\nIMPORTANT: Your previous response was not valid JSON. You MUST respond with ONLY a JSON array. " +
            "No text before or after. No markdown code fences. Just the raw JSON array starting with [ and ending with ].";
    }

    /// <summary>
    /// Maps a model-returned block string ("Person"/"Provider", case-insensitive) to
    /// RequirementBlock. Returns null for anything unrecognised — an item with a null block is
    /// treated the same as one with a missing identifier or verbatim text: it cannot be matched
    /// to a map-declared feature, so it contributes nothing and cannot cause a duplicate-key
    /// crash.
    /// </summary>
    private static RequirementBlock? ParseBlock(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "person" => RequirementBlock.Person,
        "provider" => RequirementBlock.Provider,
        _ => null
    };

    /// <summary>
    /// Returns the map-declared (block, identifier) pairs for <paramref name="standard"/> that
    /// don't appear — with a non-empty verbatim text — anywhere in <paramref name="extracted"/>.
    /// Matching is an exact (identifier, block) pair comparison, not the old regex substring
    /// search against free-text Section — the extraction schema now returns a structured
    /// identifier + block per item, so there is no ambiguity to guard against.
    /// </summary>
    private static List<string> FindMissingFeatures(StructureStandard standard, List<ExtractedFeature> extracted)
    {
        var actual = new HashSet<(string Identifier, RequirementBlock Block)>();
        foreach (var item in extracted)
        {
            var block = ParseBlock(item.Block);
            if (block == null || string.IsNullOrWhiteSpace(item.Identifier) || string.IsNullOrWhiteSpace(item.VerbatimText))
                continue;

            actual.Add((item.Identifier.Trim(), block.Value));
        }

        var missing = new List<string>();
        foreach (var feature in standard.Features)
        {
            if (!actual.Contains((feature.Identifier, feature.Block)))
                missing.Add($"{feature.Block} {feature.Identifier}");
        }

        return missing;
    }

    /// <summary>
    /// Priority order used to pick the single LastIngestionErrorCode when failed segments have
    /// different failure reasons, ranked least to most diagnosable on its own: InvalidJson means
    /// no parseable output was returned at all; Truncated means some output arrived but hit the
    /// hard token limit; Incomplete means the model understood the task and returned valid,
    /// parseable JSON that was merely missing a declared feature. The highest-ranked reason
    /// present wins the single-valued code. This ranking only affects which code is stored — the
    /// full per-standard detail for every failure always lands in LastIngestionErrorMessage
    /// regardless of which code wins here.
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
    /// MapSegmentFailures, not repeated per standard.
    /// </summary>
    private static string DescribeSegmentReason(SegmentExtractionOutcome outcome) => outcome.Failure switch
    {
        SegmentFailureReason.Truncated =>
            $"response was truncated (stop_reason=max_tokens) on both the initial attempt and the retry - the {MaxTokens}-token output limit was too small for this segment",

        SegmentFailureReason.InvalidJson =>
            "response could not be parsed as valid JSON on either the initial attempt or the retry",

        SegmentFailureReason.Incomplete =>
            $"response was missing declared features ({string.Join(", ", outcome.MissingFeatures ?? new List<string>())}) after a retry - the segment appears incomplete rather than truncated",

        _ => "failed for an unrecognised reason"
    };

    /// <summary>
    /// Builds the aggregated failure code/message across every standard segment that failed
    /// (run-all-collect-failures: every standard is attempted before this is called, see
    /// ExecuteAsync). Two shapes:
    ///
    /// - Common root cause: every failed segment failed for the identical reason (e.g. the whole
    ///   document's text is malformed and every standard truncates identically). Reads as one
    ///   failure story naming all affected standards, not N unrelated failures.
    /// - Mixed reasons: failed segments enumerated individually, each naming its own standard
    ///   and reason, so no distinct failure is lost to the single-valued error code.
    /// </summary>
    private static (string ErrorCode, string ErrorMessage) MapSegmentFailures(
        IReadOnlyList<SegmentExtractionOutcome> failedSegments, int totalSegments)
    {
        var distinctReasons = failedSegments.Select(f => f.Failure).Distinct().ToList();
        var primaryReason = distinctReasons.Count == 1
            ? distinctReasons[0]
            : ReasonPriority.First(distinctReasons.Contains);
        var errorCode = ErrorCodeFor(primaryReason);

        if (distinctReasons.Count == 1 && failedSegments.Count > 1)
        {
            var standards = string.Join(", ", failedSegments.Select(f => $"{f.Standard.Id} (P{f.PrincipleNumber})"));
            var sharedReason = DescribeSegmentReason(failedSegments[0]);
            return (errorCode,
                $"All {failedSegments.Count} of {totalSegments} standards ({standards}) failed for the same reason: {sharedReason}. No requirements were persisted for any standard.");
        }

        var perStandardDetails = string.Join("; ", failedSegments.Select(f =>
            $"Standard {f.Standard.Id} (Principle {f.PrincipleNumber}): {DescribeSegmentReason(f)}"));

        return (errorCode,
            $"{failedSegments.Count} of {totalSegments} standards failed extraction. {perStandardDetails}. No requirements were persisted for any standard.");
    }

    /// <summary>
    /// Builds the error code/message for a failed MapCoverageVerifier result. Priority when more
    /// than one category is non-empty (rare — under today's map-driven assembly, missing is the
    /// only category that can actually fire via the real pipeline, since the per-segment check
    /// above already prevents an incomplete segment from reaching assembly, and assembly itself
    /// cannot invent unexpected or misattributed candidates; MapCoverageVerifier's unexpected/
    /// misattributed detection exists as an explicit, independently-tested safety net rather than
    /// a currently-reachable path): misattributed first, since wrong attribution is the more
    /// actionable defect to surface distinctly; then unexpected; then missing. Every category
    /// present is still named in full in the message regardless of which one wins the
    /// single-valued code — mirrors MapSegmentFailures' approach for per-segment failures.
    /// </summary>
    private static (string ErrorCode, string ErrorMessage) DescribeCoverageFailure(MapCoverageResult coverage)
    {
        var parts = new List<string>();

        if (coverage.Missing.Count > 0)
        {
            parts.Add(
                $"{coverage.Missing.Count} map-declared feature(s) missing from the transcription: " +
                string.Join(", ", coverage.Missing));
        }

        if (coverage.Unexpected.Count > 0)
        {
            parts.Add(
                $"{coverage.Unexpected.Count} feature(s) not declared anywhere in the structure map: " +
                string.Join(", ", coverage.Unexpected));
        }

        if (coverage.Misattributed.Count > 0)
        {
            var misattributedDetails = string.Join(", ", coverage.Misattributed.Select(f =>
                $"{f.Block} {f.Identifier} (map declares Standard {f.DeclaredStandardId}/Principle {f.DeclaredPrincipleNumber}, transcribed as Standard {f.ActualStandardId}/Principle {f.ActualPrincipleNumber})"));
            parts.Add($"{coverage.Misattributed.Count} feature(s) misattributed: {misattributedDetails}");
        }

        var errorCode = coverage.Misattributed.Count > 0
            ? "extraction_misattributed"
            : coverage.Unexpected.Count > 0
                ? "extraction_unexpected"
                : "extraction_incomplete";

        var message =
            $"Feature-level completeness check against the structure map failed: {string.Join("; ", parts)}. No requirements were persisted.";

        return (errorCode, message);
    }

    private static string BuildExtractionPrompt(string documentText, int principleNumber, StructureStandard standard)
    {
        var personLines = string.Join("\n", standard.PersonFeatures.Select(f => $"- {f.Identifier}"));
        var providerLines = string.Join("\n", standard.ProviderFeatures.Select(f => $"- {f.Identifier}"));
        var totalCount = standard.Features.Count;

        return $@"You are transcribing verbatim text from a regulatory document. A structure map — authored and reviewed separately, NOT by you — has already determined exactly which features this document defines for Standard {standard.Id} (Principle {principleNumber}). Your ONLY task is to locate each declared feature's exact printed text in the document below and copy it. Do not judge whether a feature belongs, do not paraphrase, summarise, reorder, merge, or omit any of them.

The structure map declares these features for Standard {standard.Id}:

Person-experience features (numbered items describing what the person receiving the service experiences):
{personLines}

Provider features (numbered items describing what the service provider must have in place):
{providerLines}

For EACH of the {totalCount} features listed above, find its exact verbatim text in the document text below (search near ""Standard {standard.Id}"" and its person-experience/provider feature lists) and transcribe it exactly as printed. Exclude footnote marker glyphs and footnote text itself — footnotes are handled separately from this transcription.

Respond ONLY with a valid JSON array of exactly {totalCount} objects, one per feature listed above (any order), each shaped as:
{{""identifier"": ""<exact identifier exactly as listed above, unchanged>"", ""block"": ""Person"" or ""Provider"", ""verbatimText"": ""<the feature's exact printed text>""}}

IMPORTANT RULES:
- Copy text EXACTLY as printed in the document — no paraphrasing, no summarising, no correcting grammar or punctuation
- Use the identifier strings exactly as listed above, unchanged (including any trailing punctuation)
- Do NOT invent, split, merge, or omit any feature listed above
- Do NOT include any feature that is not listed above, even if the document contains other numbered items nearby
- Respond ONLY with the JSON array — no preamble, no markdown, no explanation

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
    private List<ExtractedFeature>? TryParseFeatures(string responseText)
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

            return JsonSerializer.Deserialize<List<ExtractedFeature>>(json, CamelCaseOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Claude response as JSON: {Preview}",
                responseText.Length > 200 ? responseText[..200] : responseText);
            return null;
        }
    }

    /// <summary>
    /// One map-declared feature paired with the verbatim text its standard's segment produced
    /// for it, plus the document-order DisplayOrder assigned once every segment has succeeded.
    /// </summary>
    private sealed class TranscribedFeature
    {
        public int PrincipleNumber { get; init; }
        public string StandardId { get; init; } = string.Empty;
        public StructureFeature MapFeature { get; init; } = null!;
        public string VerbatimText { get; init; } = string.Empty;
        public int DisplayOrder { get; set; }
    }

    /// <summary>
    /// Canonical principle display labels for HIQA's four principles — closes the pre-existing
    /// gap where Principle 1 had no canonical label entry (the old model-inferred scheme only
    /// covered P2-P4). These are now deterministic lookups rather than model-composed text,
    /// matching the map-driven design: Principle/PrincipleLabel are derived facts about the
    /// document's structure, not requirement content the model judges. HIQA-specific for now —
    /// a future non-HIQA document's principle labels would need their own source (the map schema
    /// doesn't carry principle-level text today), falling back to a generic "Principle {N}" label.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, string> HiqaCanonicalPrincipleLabels =
        new Dictionary<int, string>
        {
            [1] = "A Human Rights-based Approach",
            [2] = "Safety & Wellbeing",
            [3] = "Responsiveness",
            [4] = "Accountability",
        };

    private static string PrincipleLabelFor(int principleNumber) =>
        HiqaCanonicalPrincipleLabels.TryGetValue(principleNumber, out var label) ? label : $"Principle {principleNumber}";

    private async Task<int> PersistDraftRequirementsAsync(
        RegulatoryProfile profile,
        List<TranscribedFeature> transcribedFeatures,
        bool isProvisional,
        CancellationToken cancellationToken)
    {
        // Dedup key is (FeatureIdentifier, Block), not the free-text Title the pre-rework code
        // used — Title is no longer a stable identity now that it's a deterministic truncation of
        // verbatim text (see below), and (FeatureIdentifier, Block) is a far more precise natural
        // key now that extraction is map-driven: a retry or re-ingestion that transcribes the same
        // declared feature again must be recognised as the same requirement even if the model's
        // wording differs slightly between calls.
        var existingKeys = await _dbContext.RegulatoryRequirements
            .IgnoreQueryFilters()
            .Where(r => r.RegulatoryProfileId == profile.Id && r.FeatureIdentifier != null && r.Block != null)
            .Select(r => new { r.FeatureIdentifier, r.Block })
            .ToListAsync(cancellationToken);

        var existingKeySet = new HashSet<(string Identifier, RequirementBlock Block)>(
            existingKeys.Select(k => (k.FeatureIdentifier!, k.Block!.Value)));

        var created = 0;

        foreach (var item in transcribedFeatures)
        {
            var key = (item.MapFeature.Identifier, item.MapFeature.Block);
            if (existingKeySet.Contains(key))
            {
                _logger.LogDebug("Skipping already-persisted feature {Block} {Identifier}",
                    item.MapFeature.Block, item.MapFeature.Identifier);
                continue;
            }

            var verbatimText = item.VerbatimText.Length > 2000 ? item.VerbatimText[..2000] : item.VerbatimText;

            // Title is NOT authoritative content — it is a deterministic truncation of the
            // verbatim text, kept only so existing list/detail views have something to display in
            // that slot. The feature's real content lives in VerbatimText/FeatureIdentifier/Block.
            var title = verbatimText.Length > 200 ? verbatimText[..197] + "..." : verbatimText;

            var footnote = item.MapFeature.FootnoteDefinition;
            if (footnote != null && footnote.Length > 1000)
                footnote = footnote[..1000];

            var requirement = new RegulatoryRequirement
            {
                RegulatoryProfileId = profile.Id,
                Title = title,
                // Description mirrors VerbatimText rather than an authored paraphrase — this
                // chunk does not invent a separate gloss of what the feature "entails" (that
                // would itself be non-authoritative content). Existing consumers that read
                // Description continue to work unchanged, now seeing verbatim text instead of a
                // model-composed summary.
                Description = verbatimText,
                Section = $"Standard {item.StandardId}",
                SectionLabel = null,
                Principle = $"P{item.PrincipleNumber}",
                PrincipleLabel = PrincipleLabelFor(item.PrincipleNumber),
                // No per-feature signal exists in the structure map to derive Priority from —
                // that was previously a model judgment call, which the map-driven design
                // deliberately removes. Reviewers can still adjust it when approving.
                Priority = "med",
                DisplayOrder = item.DisplayOrder,
                IngestionSource = RequirementIngestionSource.Automated,
                IngestionStatus = RequirementIngestionStatus.Draft,
                IsActive = true,
                FeatureIdentifier = item.MapFeature.Identifier,
                Block = item.MapFeature.Block,
                VerbatimText = verbatimText,
                FootnoteDefinition = footnote,
                IsProvisional = isProvisional,
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow,
            };

            _dbContext.RegulatoryRequirements.Add(requirement);
            existingKeySet.Add(key);
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
    /// Intermediate DTO for deserialization of Claude's JSON response — a transcription of one
    /// map-declared feature, not a free-judged requirement. Block is read as its raw string
    /// ("Person"/"Provider") and parsed via ParseBlock rather than a JsonStringEnumConverter, so
    /// an unrecognised value degrades to "this item can't be matched" rather than a deserialization
    /// exception that would fail the whole segment.
    /// </summary>
    private class ExtractedFeature
    {
        public string Identifier { get; set; } = string.Empty;
        public string? Block { get; set; }
        public string VerbatimText { get; set; } = string.Empty;
    }
}
