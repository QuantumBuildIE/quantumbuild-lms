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

            // Step 2 — Claude extraction
            var extractedRequirements = await ExtractRequirementsViaClaudeAsync(extractedText, regulatoryDocumentId, cancellationToken);
            if (extractedRequirements == null || extractedRequirements.Count == 0)
            {
                _logger.LogWarning("No requirements extracted from document {DocumentId}", regulatoryDocumentId);
                await MarkSucceededAsync(document, cancellationToken);
                return;
            }

            _logger.LogInformation("Extracted {Count} requirements from document {DocumentId}",
                extractedRequirements.Count, regulatoryDocumentId);

            // Step 3 — Persist as drafts for each matching profile
            var profiles = document.Profiles.Where(p => p.IsActive).ToList();
            if (profiles.Count == 0)
            {
                _logger.LogWarning("Document {DocumentId} has no active profiles", regulatoryDocumentId);
                await MarkSucceededAsync(document, cancellationToken);
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

    private async Task<List<ExtractedRequirement>?> ExtractRequirementsViaClaudeAsync(
        string documentText, Guid documentId, CancellationToken cancellationToken)
    {
        var prompt = BuildExtractionPrompt(documentText);

        // First attempt
        var responseText = await CallClaudeAsync(prompt, documentId, cancellationToken);
        var requirements = TryParseRequirements(responseText);

        if (requirements != null)
            return requirements;

        // Retry with stricter prompt
        _logger.LogWarning("First extraction attempt returned invalid JSON, retrying with stricter prompt");
        var stricterPrompt = prompt + "\n\nIMPORTANT: Your previous response was not valid JSON. You MUST respond with ONLY a JSON array. No text before or after. No markdown code fences. Just the raw JSON array starting with [ and ending with ].";

        responseText = await CallClaudeAsync(stricterPrompt, documentId, cancellationToken);
        requirements = TryParseRequirements(responseText);

        if (requirements == null)
        {
            _logger.LogError("Failed to parse requirements from Claude response after retry");
        }

        return requirements;
    }

    private static string BuildExtractionPrompt(string documentText)
    {
        return $@"You are a regulatory compliance expert. Analyse the following regulatory document and extract all requirements that relate to staff training, competency, or compliance obligations.

For each requirement, extract:
- title: A concise title (max 200 chars) for the training/competency requirement
- description: A detailed description (max 2000 chars) of what the requirement entails
- section: The section or article reference from the document (e.g. ""Standard 2.3"", ""Article 4"", ""§7""). Use the standard numbering from the source document. If not explicitly stated, use the document section heading or ""General"". This field is MANDATORY — never return null or omit it
- sectionLabel: A short descriptive label for the section (e.g. ""Incident Reporting"", ""Staff Training"", ""MAR Management""). Derive from context if not explicit. This field is MANDATORY — never return null or omit it
- principle: A short category label grouping this requirement (e.g. ""P2"", ""Staff Competency"", ""Food Safety Management""). If not explicitly stated in the document, infer from the requirement's subject matter. This field is MANDATORY — never return null or omit it
- principleLabel: The full description of the principle category. MUST use one of the exact canonical labels below when applicable, or derive a descriptive label from the requirement's subject matter
- priority: ""high"" for safety-critical requirements, ""med"" for standard compliance, ""low"" for best-practice/advisory
- displayOrder: Sequential numbering starting from 1

CANONICAL PRINCIPLE LABELS (use these exact strings — do not paraphrase or reword):
- P2 — ""Safety & Wellbeing""
- P3 — ""Responsiveness""
- P4 — ""Accountability""

If the document uses different wording (e.g. ""Safety and Wellbeing""), map it to the canonical label above.
If the principle does not match any of these, set principleLabel to the document's exact text.

IMPORTANT RULES:
- Extract ONLY requirements related to staff training, competency, skills, or compliance obligations
- Do NOT include general policy statements, organisational structure requirements, or non-training items
- Each requirement should be actionable as a training topic
- The fields section, sectionLabel, principle, and principleLabel are ALL MANDATORY — never return null or omit them. If the document does not explicitly state them, infer reasonable values from the requirement's context and subject matter
- Respond ONLY with a valid JSON array — no preamble, no markdown, no explanation

DOCUMENT TEXT:
{documentText}";
    }

    private async Task<string> CallClaudeAsync(string prompt, Guid documentId, CancellationToken cancellationToken)
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

        return parsed.ContentText;
    }

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

            var requirements = JsonSerializer.Deserialize<List<ExtractedRequirement>>(json, CamelCaseOptions);
            return requirements?.Count > 0 ? requirements : null;
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
