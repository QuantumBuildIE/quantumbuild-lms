using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Background job that fetches a regulatory document, extracts text via AI,
/// and persists draft RegulatoryRequirement records for SuperUser review.
/// </summary>
public class RequirementIngestionJob
{
    private const string SonnetModel = "claude-sonnet-4-20250514";
    private const int MaxTokens = 8192;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IPdfExtractionService _pdfExtractionService;
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<RequirementIngestionJob> _logger;

    public RequirementIngestionJob(
        IToolboxTalksDbContext dbContext,
        IPdfExtractionService pdfExtractionService,
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<RequirementIngestionJob> logger)
    {
        _dbContext = dbContext;
        _pdfExtractionService = pdfExtractionService;
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    [Queue("content-generation")]
    public async Task ExecuteAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting requirement ingestion for document {DocumentId}", regulatoryDocumentId);

        try
        {
            // Load document with profiles
            var document = await _dbContext.RegulatoryDocuments
                .Include(d => d.Profiles)
                    .ThenInclude(p => p.Sector)
                .FirstOrDefaultAsync(d => d.Id == regulatoryDocumentId, cancellationToken);

            if (document == null)
            {
                _logger.LogError("Document {DocumentId} not found", regulatoryDocumentId);
                return;
            }

            if (string.IsNullOrWhiteSpace(document.SourceUrl))
            {
                _logger.LogError("Document {DocumentId} has no SourceUrl", regulatoryDocumentId);
                return;
            }

            // Step 1 — Fetch and extract text
            var extractedText = await FetchDocumentTextAsync(document.SourceUrl, cancellationToken);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                _logger.LogError("Failed to extract text from document {DocumentId}", regulatoryDocumentId);
                return;
            }

            _logger.LogInformation("Extracted {Length} characters from document {DocumentId}",
                extractedText.Length, regulatoryDocumentId);

            // Step 2 — Claude extraction
            var extractedRequirements = await ExtractRequirementsViaClaudeAsync(extractedText, cancellationToken);
            if (extractedRequirements == null || extractedRequirements.Count == 0)
            {
                _logger.LogWarning("No requirements extracted from document {DocumentId}", regulatoryDocumentId);
                document.LastIngestedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            _logger.LogInformation("Extracted {Count} requirements from document {DocumentId}",
                extractedRequirements.Count, regulatoryDocumentId);

            // Step 3 — Persist as drafts for each matching profile
            var profiles = document.Profiles.Where(p => p.IsActive).ToList();
            if (profiles.Count == 0)
            {
                _logger.LogWarning("Document {DocumentId} has no active profiles", regulatoryDocumentId);
                document.LastIngestedAt = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
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
            document.LastIngestedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Ingestion complete for document {DocumentId}: {Created} draft requirements created across {ProfileCount} profiles",
                regulatoryDocumentId, totalCreated, profiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Ingestion job failed for document {DocumentId}: {Message}",
                regulatoryDocumentId, ex.Message);
            // Don't rethrow — Hangfire job should not fail noisily
        }
    }

    private async Task<string?> FetchDocumentTextAsync(
        string sourceUrl, CancellationToken cancellationToken)
    {
        try
        {
            if (sourceUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                // Use existing PDF extraction service
                var result = await _pdfExtractionService.ExtractTextFromUrlAsync(sourceUrl, cancellationToken);
                if (!result.Success)
                {
                    _logger.LogError("PDF extraction failed: {Error}", result.ErrorMessage);
                    return null;
                }
                return result.Text;
            }

            // Fetch HTML and strip to plain text
            _logger.LogInformation("Fetching web page: {Url}", sourceUrl);
            var response = await _httpClient.GetAsync(sourceUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch URL {Url}: {Status}", sourceUrl, response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            return StripHtmlToText(html);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch document from {Url}", sourceUrl);
            return null;
        }
    }

    private async Task<List<ExtractedRequirement>?> ExtractRequirementsViaClaudeAsync(
        string documentText, CancellationToken cancellationToken)
    {
        var prompt = BuildExtractionPrompt(documentText);

        // First attempt
        var responseText = await CallClaudeAsync(prompt, cancellationToken);
        var requirements = TryParseRequirements(responseText);

        if (requirements != null)
            return requirements;

        // Retry with stricter prompt
        _logger.LogWarning("First extraction attempt returned invalid JSON, retrying with stricter prompt");
        var stricterPrompt = prompt + "\n\nIMPORTANT: Your previous response was not valid JSON. You MUST respond with ONLY a JSON array. No text before or after. No markdown code fences. Just the raw JSON array starting with [ and ending with ].";

        responseText = await CallClaudeAsync(stricterPrompt, cancellationToken);
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
- section: The section reference (e.g. ""§7"", ""Section 3"") if identifiable, or null
- sectionLabel: The section name/label if identifiable, or null
- principle: The principle reference if identifiable (e.g. ""P2""), or null
- principleLabel: The principle name if identifiable, or null
- priority: ""high"" for safety-critical requirements, ""med"" for standard compliance, ""low"" for best-practice/advisory
- displayOrder: Sequential numbering starting from 1

IMPORTANT RULES:
- Extract ONLY requirements related to staff training, competency, skills, or compliance obligations
- Do NOT include general policy statements, organisational structure requirements, or non-training items
- Each requirement should be actionable as a training topic
- Respond ONLY with a valid JSON array — no preamble, no markdown, no explanation

DOCUMENT TEXT:
{documentText}";
    }

    private async Task<string> CallClaudeAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = SonnetModel,
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

        return ParseClaudeResponseText(responseBody);
    }

    private static string ParseClaudeResponseText(string responseBody)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);

        if (!jsonDoc.RootElement.TryGetProperty("content", out var contentArray))
            return string.Empty;

        foreach (var item in contentArray.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var textEl))
                return textEl.GetString() ?? string.Empty;
        }

        return string.Empty;
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
