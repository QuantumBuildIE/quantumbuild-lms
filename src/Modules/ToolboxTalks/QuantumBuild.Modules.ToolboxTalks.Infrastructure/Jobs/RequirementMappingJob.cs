using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Background job that analyses published training content and suggests
/// which regulatory requirements it covers via Claude AI mapping.
/// Fire-and-forget, triggered from both PublishAsLessonAsync and PublishAsCourseAsync.
/// </summary>
public class RequirementMappingJob
{
    private const string SonnetModel = "claude-sonnet-4-20250514";
    private const int MaxTokens = 8192;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IToolboxTalksDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly IAiUsageLogger _aiUsageLogger;
    private readonly ILogger<RequirementMappingJob> _logger;

    public RequirementMappingJob(
        IToolboxTalksDbContext dbContext,
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        IAiUsageLogger aiUsageLogger,
        ILogger<RequirementMappingJob> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _settings = settings.Value;
        _aiUsageLogger = aiUsageLogger;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 1)]
    [Queue("content-generation")]
    public async Task MapRequirementsAsync(
        Guid tenantId,
        Guid? toolboxTalkId,
        Guid? courseId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting requirement mapping for tenant {TenantId}, TalkId={TalkId}, CourseId={CourseId}",
            tenantId, toolboxTalkId, courseId);

        try
        {
            // Step 1 — Load content
            var contentString = await BuildContentStringAsync(toolboxTalkId, courseId, cancellationToken);
            if (string.IsNullOrWhiteSpace(contentString))
            {
                _logger.LogWarning("No content found for mapping — TalkId={TalkId}, CourseId={CourseId}",
                    toolboxTalkId, courseId);
                return;
            }

            // Step 2 — Load approved requirements for tenant's sectors
            var requirements = await LoadApprovedRequirementsAsync(tenantId, cancellationToken);
            if (requirements.Count == 0)
            {
                _logger.LogInformation("No approved requirements found for tenant {TenantId} sectors", tenantId);
                return;
            }

            _logger.LogInformation("Loaded {ContentLength} chars of content and {RequirementCount} requirements",
                contentString.Length, requirements.Count);

            // Step 3 — Claude mapping
            var suggestions = await MapViaClaudeAsync(contentString, requirements, tenantId, toolboxTalkId ?? courseId, cancellationToken);
            if (suggestions == null || suggestions.Count == 0)
            {
                _logger.LogInformation("Claude returned no mapping suggestions");
                return;
            }

            _logger.LogInformation("Claude suggested {Count} mappings", suggestions.Count);

            // Step 4 — Check existing and persist
            var created = await PersistMappingsAsync(
                tenantId, toolboxTalkId, courseId, suggestions, cancellationToken);

            _logger.LogInformation(
                "Requirement mapping complete: {Created} new mappings created for tenant {TenantId}",
                created, tenantId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Requirement mapping job failed for tenant {TenantId}, TalkId={TalkId}, CourseId={CourseId}: {Message}",
                tenantId, toolboxTalkId, courseId, ex.Message);
            // Don't rethrow — Hangfire job should not fail noisily
        }
    }

    private async Task<string?> BuildContentStringAsync(
        Guid? toolboxTalkId, Guid? courseId, CancellationToken cancellationToken)
    {
        if (toolboxTalkId.HasValue)
        {
            var talk = await _dbContext.ToolboxTalks
                .IgnoreQueryFilters()
                .Include(t => t.Sections)
                .FirstOrDefaultAsync(t => t.Id == toolboxTalkId.Value && !t.IsDeleted, cancellationToken);

            if (talk == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine($"Title: {talk.Title}");
            sb.AppendLine($"Description: {talk.Description}");
            foreach (var section in talk.Sections.Where(s => !s.IsDeleted).OrderBy(s => s.SectionNumber))
            {
                sb.AppendLine($"Section: {section.Title}");
                sb.AppendLine(StripHtml(section.Content));
            }
            return sb.ToString();
        }

        if (courseId.HasValue)
        {
            var course = await _dbContext.ToolboxTalkCourses
                .IgnoreQueryFilters()
                .Include(c => c.CourseItems)
                    .ThenInclude(ci => ci.ToolboxTalk)
                        .ThenInclude(t => t.Sections)
                .FirstOrDefaultAsync(c => c.Id == courseId.Value && !c.IsDeleted, cancellationToken);

            if (course == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine($"Course Title: {course.Title}");
            sb.AppendLine($"Course Description: {course.Description}");
            foreach (var item in course.CourseItems.OrderBy(ci => ci.OrderIndex))
            {
                var talk = item.ToolboxTalk;
                if (talk == null || talk.IsDeleted) continue;
                sb.AppendLine($"Talk: {talk.Title}");
                foreach (var section in talk.Sections.Where(s => !s.IsDeleted).OrderBy(s => s.SectionNumber))
                {
                    sb.AppendLine($"Section: {section.Title}");
                    sb.AppendLine(StripHtml(section.Content));
                }
            }
            return sb.ToString();
        }

        return null;
    }

    private async Task<List<RegulatoryRequirement>> LoadApprovedRequirementsAsync(
        Guid tenantId, CancellationToken cancellationToken)
    {
        // Load tenant's sector keys
        var sectorKeys = await _dbContext.TenantSectors
            .IgnoreQueryFilters()
            .Where(ts => ts.TenantId == tenantId && !ts.IsDeleted)
            .Include(ts => ts.Sector)
            .Select(ts => ts.Sector.Key)
            .ToListAsync(cancellationToken);

        if (sectorKeys.Count == 0) return [];

        // Load profile IDs matching tenant's sectors
        var profileIds = await _dbContext.RegulatoryProfiles
            .Where(p => sectorKeys.Contains(p.SectorKey) && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (profileIds.Count == 0) return [];

        // Load approved, active requirements for those profiles
        return await _dbContext.RegulatoryRequirements
            .IgnoreQueryFilters()
            .Where(r => profileIds.Contains(r.RegulatoryProfileId)
                        && r.IngestionStatus == RequirementIngestionStatus.Approved
                        && r.IsActive
                        && !r.IsDeleted)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<MappingSuggestion>?> MapViaClaudeAsync(
        string contentString, List<RegulatoryRequirement> requirements, Guid tenantId, Guid? referenceEntityId, CancellationToken cancellationToken)
    {
        var prompt = BuildMappingPrompt(contentString, requirements);

        // First attempt
        var responseText = await CallClaudeAsync(prompt, tenantId, referenceEntityId, cancellationToken);
        var suggestions = TryParseSuggestions(responseText);
        if (suggestions != null) return suggestions;

        // Retry with stricter prompt
        _logger.LogWarning("First mapping attempt returned invalid JSON, retrying with stricter prompt");
        var stricterPrompt = prompt + "\n\nIMPORTANT: Your previous response was not valid JSON. You MUST respond with ONLY a JSON array. No text before or after. No markdown code fences. Just the raw JSON array starting with [ and ending with ].";

        responseText = await CallClaudeAsync(stricterPrompt, tenantId, referenceEntityId, cancellationToken);
        suggestions = TryParseSuggestions(responseText);

        if (suggestions == null)
            _logger.LogError("Failed to parse mapping suggestions from Claude response after retry");

        return suggestions;
    }

    private static string BuildMappingPrompt(string contentString, List<RegulatoryRequirement> requirements)
    {
        var requirementsList = new StringBuilder();
        foreach (var req in requirements)
        {
            requirementsList.AppendLine($"ID: {req.Id}");
            requirementsList.AppendLine($"Title: {req.Title}");
            requirementsList.AppendLine($"Description: {req.Description}");
            requirementsList.AppendLine($"Section: {req.Section} — {req.SectionLabel}");
            requirementsList.AppendLine();
        }

        return $@"You are a regulatory compliance analyst. You will be given a piece of training content and a list of regulatory requirements.

Your task is to identify which requirements this training content addresses, either fully or partially.

TRAINING CONTENT:
{contentString}

REGULATORY REQUIREMENTS:
{requirementsList}

For each requirement that this content addresses, provide:
- requirementId: the exact ID from the list
- confidenceScore: 0-100 (how well the content addresses this requirement)
- reasoning: one sentence explaining why this content addresses the requirement

Only include requirements that are genuinely addressed by the content. Do not include requirements where there is no meaningful connection.

Respond ONLY with a valid JSON array. No preamble, no markdown, no explanation outside the JSON.

Example format:
[
  {{
    ""requirementId"": ""guid-here"",
    ""confidenceScore"": 85,
    ""reasoning"": ""The content covers safeguarding awareness training including recognition and reporting of suspected abuse.""
  }}
]

If no requirements are addressed, respond with an empty array: []";
    }

    private async Task<string> CallClaudeAsync(string prompt, Guid tenantId, Guid? referenceEntityId, CancellationToken cancellationToken)
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

        var parsed = AnthropicResponseParser.Parse(responseBody);

        await _aiUsageLogger.LogAsync(
            tenantId,
            AiOperationCategory.RequirementMapping,
            parsed.Model,
            parsed.InputTokens,
            parsed.OutputTokens,
            isSystemCall: true,
            userId: null,
            referenceEntityId: referenceEntityId,
            cancellationToken);

        return parsed.ContentText;
    }

    private List<MappingSuggestion>? TryParseSuggestions(string responseText)
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

            var suggestions = JsonSerializer.Deserialize<List<MappingSuggestion>>(json, CamelCaseOptions);
            return suggestions?.Count > 0 ? suggestions : null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Claude mapping response as JSON: {Preview}",
                responseText.Length > 200 ? responseText[..200] : responseText);
            return null;
        }
    }

    private async Task<int> PersistMappingsAsync(
        Guid tenantId,
        Guid? toolboxTalkId,
        Guid? courseId,
        List<MappingSuggestion> suggestions,
        CancellationToken cancellationToken)
    {
        // Load existing mappings for this content (include soft-deleted for restore-on-reassign)
        var existingMappings = await _dbContext.RegulatoryRequirementMappings
            .IgnoreQueryFilters()
            .Where(m => m.TenantId == tenantId
                        && (toolboxTalkId.HasValue ? m.ToolboxTalkId == toolboxTalkId : m.CourseId == courseId))
            .ToListAsync(cancellationToken);

        var created = 0;

        foreach (var suggestion in suggestions)
        {
            if (!Guid.TryParse(suggestion.RequirementId, out var requirementId))
            {
                _logger.LogWarning("Invalid requirement ID in suggestion: {Id}", suggestion.RequirementId);
                continue;
            }

            var existing = existingMappings.FirstOrDefault(m => m.RegulatoryRequirementId == requirementId);

            if (existing != null)
            {
                if (existing.IsDeleted)
                {
                    // Restore soft-deleted mapping (restore-on-reassign pattern)
                    existing.IsDeleted = false;
                    existing.MappingStatus = RequirementMappingStatus.Suggested;
                    existing.ConfidenceScore = suggestion.ConfidenceScore;
                    existing.AiReasoning = suggestion.Reasoning;
                    existing.ReviewNotes = null;
                    existing.ReviewedBy = null;
                    existing.ReviewedAt = null;
                    created++;
                    _logger.LogDebug("Restored soft-deleted mapping for requirement {RequirementId}", requirementId);
                }
                else
                {
                    // Active mapping exists (Suggested, Confirmed, or Rejected) — skip
                    _logger.LogDebug("Skipping existing mapping for requirement {RequirementId} (status: {Status})",
                        requirementId, existing.MappingStatus);
                }
                continue;
            }

            // Create new mapping
            var mapping = new RegulatoryRequirementMapping
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RegulatoryRequirementId = requirementId,
                ToolboxTalkId = toolboxTalkId,
                CourseId = courseId,
                MappingStatus = RequirementMappingStatus.Suggested,
                ConfidenceScore = suggestion.ConfidenceScore,
                AiReasoning = suggestion.Reasoning,
            };

            _dbContext.RegulatoryRequirementMappings.Add(mapping);
            created++;
        }

        if (created > 0)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return created;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var text = System.Text.RegularExpressions.Regex.Replace(html, @"<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    /// <summary>
    /// Intermediate DTO for deserialization of Claude's JSON response
    /// </summary>
    private class MappingSuggestion
    {
        public string RequirementId { get; set; } = string.Empty;
        public int ConfidenceScore { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }
}
