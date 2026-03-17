using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Scores translation validation runs against regulatory standards using Claude Sonnet.
/// Three scoring methods: source document quality, pure linguistic translation, and
/// regulatory-aware translation scoring with sector-specific criteria.
/// </summary>
public class RegulatoryScoreService : IRegulatoryScoreService
{
    private const string SonnetModel = "claude-sonnet-4-20250514";
    private const int MaxTokens = 4096;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IToolboxTalksDbContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<RegulatoryScoreService> _logger;

    public RegulatoryScoreService(
        IToolboxTalksDbContext dbContext,
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<RegulatoryScoreService> logger)
    {
        _dbContext = dbContext;
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<RegulatoryScoreResultDto> ScoreAsync(
        Guid validationRunId,
        ValidationScoreType scoreType,
        CancellationToken cancellationToken = default)
    {
        var run = await _dbContext.TranslationValidationRuns
            .FirstOrDefaultAsync(r => r.Id == validationRunId, cancellationToken)
            ?? throw new InvalidOperationException($"Validation run {validationRunId} not found");

        var results = await _dbContext.TranslationValidationResults
            .Where(r => r.ValidationRunId == validationRunId && r.FinalScore > 0)
            .OrderBy(r => r.SectionIndex)
            .ToListAsync(cancellationToken);

        if (results.Count == 0)
            throw new InvalidOperationException("No completed sections found for this validation run");

        // Load regulatory profile if needed
        RegulatoryProfile? profile = null;
        RegulatoryBody? body = null;
        List<RegulatoryCriteria>? criteria = null;

        if (scoreType != ValidationScoreType.PureTranslation && !string.IsNullOrWhiteSpace(run.SectorKey))
        {
            profile = await _dbContext.RegulatoryProfiles
                .Include(p => p.RegulatoryDocument)
                    .ThenInclude(d => d.RegulatoryBody)
                .FirstOrDefaultAsync(p => p.SectorKey == run.SectorKey && p.IsActive, cancellationToken);

            if (profile != null)
            {
                body = profile.RegulatoryDocument.RegulatoryBody;

                // Load criteria: system defaults first, tenant overrides where available
                var allCriteria = await _dbContext.RegulatoryCriteria
                    .Where(c => c.RegulatoryProfileId == profile.Id && c.IsActive)
                    .ToListAsync(cancellationToken);

                // SafetyGlossary pattern: tenant overrides replace system defaults per category+order
                var systemCriteria = allCriteria.Where(c => c.TenantId == null).ToList();
                var tenantCriteria = allCriteria.Where(c => c.TenantId == run.TenantId).ToList();

                criteria = new List<RegulatoryCriteria>();
                foreach (var sc in systemCriteria)
                {
                    var tenantOverride = tenantCriteria.FirstOrDefault(
                        tc => tc.CategoryKey == sc.CategoryKey && tc.DisplayOrder == sc.DisplayOrder);
                    criteria.Add(tenantOverride ?? sc);
                }
                // Add any tenant criteria that don't match system defaults
                foreach (var tc in tenantCriteria)
                {
                    if (!criteria.Any(c => c.Id == tc.Id))
                        criteria.Add(tc);
                }
            }
        }

        // Parse category weights from profile
        var categoryWeights = ParseCategoryWeights(profile?.CategoryWeightsJson);

        // Call the appropriate scoring method
        var (response, overallScore, verdict, summary, categoryScores) = scoreType switch
        {
            ValidationScoreType.SourceDocument => await ScoreSourceDocumentAsync(
                results, categoryWeights, criteria, body?.Code, cancellationToken),
            ValidationScoreType.PureTranslation => await ScorePureTranslationAsync(
                results, cancellationToken),
            ValidationScoreType.RegulatoryTranslation => await ScoreRegulatoryTranslationAsync(
                results, categoryWeights, criteria, body?.Code, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(scoreType))
        };

        // Determine run number
        var existingCount = await _dbContext.ValidationRegulatoryScores
            .CountAsync(s => s.ValidationRunId == validationRunId
                && s.ScoreType == scoreType, cancellationToken);
        var runNumber = existingCount + 1;

        // Determine run label
        var runLabel = scoreType switch
        {
            ValidationScoreType.SourceDocument => "Source Assessment",
            ValidationScoreType.PureTranslation => "Linguistic Assessment",
            ValidationScoreType.RegulatoryTranslation => runNumber == 1
                ? "Pre-Remediation Baseline"
                : $"Post-Remediation Pass {runNumber - 1}",
            _ => "Assessment"
        };

        // Persist
        var score = new ValidationRegulatoryScore
        {
            Id = Guid.NewGuid(),
            TenantId = run.TenantId,
            ValidationRunId = validationRunId,
            ScoreType = scoreType,
            RegulatoryProfileId = profile?.Id,
            OverallScore = overallScore,
            CategoryScoresJson = JsonSerializer.Serialize(categoryScores, CamelCaseOptions),
            Verdict = verdict,
            Summary = summary,
            RunLabel = runLabel,
            RunNumber = runNumber,
            FullResponseJson = response,
            ScoredSectionCount = results.Count,
            TargetLanguage = run.LanguageCode,
            RegulatoryBody = body?.Code
        };

        _dbContext.ValidationRegulatoryScores.Add(score);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Regulatory score saved: RunId={RunId}, Type={ScoreType}, Score={Score}, Verdict={Verdict}",
            validationRunId, scoreType, overallScore, verdict);

        // Build comparison delta
        int? comparisonDelta = null;
        if (runNumber > 1)
        {
            var previous = await _dbContext.ValidationRegulatoryScores
                .Where(s => s.ValidationRunId == validationRunId
                    && s.ScoreType == scoreType
                    && s.RunNumber == runNumber - 1)
                .Select(s => (int?)s.OverallScore)
                .FirstOrDefaultAsync(cancellationToken);

            if (previous.HasValue)
                comparisonDelta = overallScore - previous.Value;
        }

        return new RegulatoryScoreResultDto
        {
            Id = score.Id,
            ScoreType = scoreType,
            OverallScore = overallScore,
            Verdict = verdict,
            Summary = summary,
            CategoryScores = categoryScores,
            RunLabel = runLabel,
            RunNumber = runNumber,
            RegulatoryBody = body?.Code,
            ScoreLabel = profile?.ScoreLabel,
            ScoredSectionCount = results.Count,
            TargetLanguage = run.LanguageCode,
            CreatedAt = score.CreatedAt,
            ComparisonDelta = comparisonDelta,
            FullResponse = score.FullResponseJson
        };
    }

    public async Task<RegulatoryScoreHistoryDto> GetScoreHistoryAsync(
        Guid validationRunId,
        CancellationToken cancellationToken = default)
    {
        var scores = await _dbContext.ValidationRegulatoryScores
            .Where(s => s.ValidationRunId == validationRunId)
            .OrderBy(s => s.RunNumber)
            .ToListAsync(cancellationToken);

        // Load profile for ScoreLabel
        RegulatoryProfile? profile = null;
        var firstWithProfile = scores.FirstOrDefault(s => s.RegulatoryProfileId != null);
        if (firstWithProfile != null)
        {
            profile = await _dbContext.RegulatoryProfiles
                .FirstOrDefaultAsync(p => p.Id == firstWithProfile.RegulatoryProfileId, cancellationToken);
        }

        var allDtos = scores.Select((s, idx) => MapToDto(s, profile, scores)).ToList();

        return new RegulatoryScoreHistoryDto
        {
            ValidationRunId = validationRunId,
            SourceScore = allDtos.LastOrDefault(d => d.ScoreType == ValidationScoreType.SourceDocument),
            PureScore = allDtos.LastOrDefault(d => d.ScoreType == ValidationScoreType.PureTranslation),
            RegulatoryScores = allDtos
                .Where(d => d.ScoreType == ValidationScoreType.RegulatoryTranslation)
                .OrderBy(d => d.RunNumber)
                .ToList()
        };
    }

    #region Private Scoring Methods

    private async Task<(string Response, int OverallScore, string Verdict, string Summary, List<CategoryScoreDto> CategoryScores)>
        ScoreSourceDocumentAsync(
            List<TranslationValidationResult> results,
            List<CategoryWeight> categoryWeights,
            List<RegulatoryCriteria>? criteria,
            string? bodyCode,
            CancellationToken cancellationToken)
    {
        var sectionsText = new StringBuilder();
        for (var i = 0; i < results.Count; i++)
        {
            sectionsText.AppendLine($"--- SECTION {i + 1}: {results[i].SectionTitle} ---");
            sectionsText.AppendLine(results[i].OriginalText);
            sectionsText.AppendLine();
        }

        var categoriesBlock = BuildCategoriesBlock(categoryWeights);
        var criteriaBlock = BuildCriteriaBlock(criteria);

        var prompt = $"""
            You are a regulatory compliance assessor for workplace safety training documents.

            TASK: Score the following SOURCE DOCUMENT against the regulatory standard below. You are assessing the quality and compliance of the original document — NOT a translation.

            REGULATORY STANDARD: {bodyCode ?? "General Safety"}
            {criteriaBlock}

            SCORING CATEGORIES (use these exact keys):
            {categoriesBlock}

            CALIBRATION — use the full 0–100 range:
            - 90–100: Excellent — meets or exceeds all regulatory requirements
            - 80–89: Good professional standard — minor gaps only
            - 70–79: Functional, some issues — missing or weak regulatory language
            - 55–69: Below standard — significant gaps in regulatory compliance
            - Below 55: Significant failures — document does not meet regulatory requirements

            SOURCE DOCUMENT:
            {sectionsText}

            RESPONSE FORMAT (plain text, no markdown):
            For each category, write:
            CATEGORY_KEY: [score]
            [Brief findings for this category]

            Then write:
            OVERALL_SOURCE_SCORE: [n]
            VERDICT: [one of: MEETS REGULATORY STANDARD, MINOR ISSUES, REQUIRES REVISION, DOES NOT MEET STANDARD]
            SUMMARY: [2-3 sentence summary of the assessment]
            """;

        var response = await CallClaudeAsync(prompt, cancellationToken);
        var categoryKeys = categoryWeights.Select(c => c.Key).ToList();
        var parsed = ParseScoreResponse(response, categoryKeys, categoryWeights, "OVERALL_SOURCE_SCORE");

        return (response, parsed.OverallScore, parsed.Verdict, parsed.Summary, parsed.CategoryScores);
    }

    private async Task<(string Response, int OverallScore, string Verdict, string Summary, List<CategoryScoreDto> CategoryScores)>
        ScorePureTranslationAsync(
            List<TranslationValidationResult> results,
            CancellationToken cancellationToken)
    {
        // Fixed five categories for pure linguistic assessment
        var pureCategories = new List<CategoryWeight>
        {
            new("ACCURACY", "Accuracy", 2.0m),
            new("FLUENCY", "Fluency", 1.5m),
            new("COMPLETENESS", "Completeness", 1.5m),
            new("CONSISTENCY", "Consistency", 1.0m),
            new("STYLE", "Style", 1.0m)
        };

        var sectionsText = new StringBuilder();
        for (var i = 0; i < results.Count; i++)
        {
            sectionsText.AppendLine($"--- SECTION {i + 1}: {results[i].SectionTitle} ---");
            sectionsText.AppendLine($"SOURCE: {results[i].OriginalText}");
            sectionsText.AppendLine($"TRANSLATION: {results[i].TranslatedText}");
            sectionsText.AppendLine();
        }

        var categoriesBlock = BuildCategoriesBlock(pureCategories);

        var prompt = $"""
            You are a professional translation quality assessor.

            TASK: Score the translation quality of the following sections. This is a PURELY LINGUISTIC assessment — no regulatory overlay. Assess how well the translation conveys the meaning of the source text.

            SCORING CATEGORIES (use these exact keys):
            {categoriesBlock}

            CALIBRATION — use the full 0–100 range:
            - 90–100: Excellent — near-native quality, publication-ready
            - 80–89: Good professional standard — minor issues only
            - 70–79: Functional, some issues — meaning preserved but style/fluency lacking
            - 55–69: Below standard — meaning partially lost or significant fluency issues
            - Below 55: Significant failures — meaning substantially altered or incomprehensible

            SECTIONS:
            {sectionsText}

            RESPONSE FORMAT (plain text, no markdown):
            For each category, write:
            CATEGORY_KEY: [score]
            [Brief findings for this category]

            Then write:
            OVERALL_PURE_SCORE: [n]
            VERDICT: [one of: EXCELLENT, GOOD, ACCEPTABLE, NEEDS REVISION, POOR]
            SUMMARY: [2-3 sentence summary of the translation quality]
            """;

        var response = await CallClaudeAsync(prompt, cancellationToken);
        var categoryKeys = pureCategories.Select(c => c.Key).ToList();
        var parsed = ParseScoreResponse(response, categoryKeys, pureCategories, "OVERALL_PURE_SCORE");

        return (response, parsed.OverallScore, parsed.Verdict, parsed.Summary, parsed.CategoryScores);
    }

    private async Task<(string Response, int OverallScore, string Verdict, string Summary, List<CategoryScoreDto> CategoryScores)>
        ScoreRegulatoryTranslationAsync(
            List<TranslationValidationResult> results,
            List<CategoryWeight> categoryWeights,
            List<RegulatoryCriteria>? criteria,
            string? bodyCode,
            CancellationToken cancellationToken)
    {
        var sectionsText = new StringBuilder();
        for (var i = 0; i < results.Count; i++)
        {
            sectionsText.AppendLine($"--- SECTION {i + 1}: {results[i].SectionTitle} ---");
            sectionsText.AppendLine($"SOURCE: {results[i].OriginalText}");
            sectionsText.AppendLine($"TRANSLATION: {results[i].TranslatedText}");
            sectionsText.AppendLine();
        }

        var categoriesBlock = BuildCategoriesBlock(categoryWeights);
        var criteriaBlock = BuildCriteriaBlock(criteria);
        var responseKey = $"OVERALL_{bodyCode ?? "REG"}_SCORE";

        var prompt = $"""
            You are a regulatory compliance translation assessor for workplace safety training documents.

            TASK: Score the TRANSLATION of the following sections against the regulatory standard below. You are assessing whether the translation faithfully and completely conveys the regulatory requirements of the source document.

            CRITICAL RULE: If a weakness exists in the SOURCE DOCUMENT (e.g., vague language, missing detail), do NOT penalise the translation for faithfully reflecting that weakness. Only penalise translator-introduced problems — omissions, mistranslations, softened imperative language, or lost regulatory terminology.

            REGULATORY STANDARD: {bodyCode ?? "General Safety"}
            {criteriaBlock}

            SCORING CATEGORIES (use these exact keys):
            {categoriesBlock}

            CALIBRATION — use the full 0–100 range:
            - 90–100: Excellent — translation fully preserves all regulatory requirements
            - 80–89: Good professional standard — minor translation issues, regulatory intent preserved
            - 70–79: Functional, some issues — some regulatory nuance lost in translation
            - 55–69: Below standard — regulatory requirements partially lost or weakened
            - Below 55: Significant failures — critical regulatory content mistranslated or omitted

            SECTIONS:
            {sectionsText}

            RESPONSE FORMAT (plain text, no markdown):
            For each category, write:
            CATEGORY_KEY: [score]
            [Brief findings for this category]

            Then write:
            {responseKey}: [n]
            VERDICT: [one of: APPROVED FOR DISTRIBUTION, APPROVED WITH MINOR CORRECTIONS, REQUIRES REVISION, NOT APPROVED]
            SUMMARY: [2-3 sentence summary of the regulatory translation assessment]
            """;

        var response = await CallClaudeAsync(prompt, cancellationToken);
        var categoryKeys = categoryWeights.Select(c => c.Key).ToList();
        var parsed = ParseScoreResponse(response, categoryKeys, categoryWeights, responseKey);

        return (response, parsed.OverallScore, parsed.Verdict, parsed.Summary, parsed.CategoryScores);
    }

    #endregion

    #region Claude API

    private async Task<string> CallClaudeAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.Claude.ApiKey))
            throw new InvalidOperationException("Claude API key not configured");

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
            _logger.LogError(
                "Claude regulatory scoring failed: {StatusCode} - {Response}",
                response.StatusCode, responseBody);
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

    #endregion

    #region Response Parsing

    private record ParsedScore(int OverallScore, string Verdict, string Summary, List<CategoryScoreDto> CategoryScores);

    private ParsedScore ParseScoreResponse(
        string response,
        List<string> categoryKeys,
        List<CategoryWeight> categoryWeights,
        string overallScoreKey)
    {
        var categoryScores = new List<CategoryScoreDto>();

        // Extract per-category scores
        foreach (var weight in categoryWeights)
        {
            var pattern = $@"{Regex.Escape(weight.Key)}:\s*(\d+)";
            var match = Regex.Match(response, pattern, RegexOptions.IgnoreCase);
            var score = match.Success ? int.Parse(match.Groups[1].Value) : 0;

            categoryScores.Add(new CategoryScoreDto
            {
                Key = weight.Key,
                Label = weight.Label,
                Weight = weight.Weight,
                Score = Math.Clamp(score, 0, 100)
            });
        }

        // Extract overall score
        var overallPattern = $@"{Regex.Escape(overallScoreKey)}:\s*(\d+)";
        var overallMatch = Regex.Match(response, overallPattern, RegexOptions.IgnoreCase);
        int overallScore;

        if (overallMatch.Success)
        {
            overallScore = Math.Clamp(int.Parse(overallMatch.Groups[1].Value), 0, 100);
        }
        else
        {
            // Fallback: weighted average
            overallScore = CalculateWeightedAverage(categoryScores);
            _logger.LogWarning(
                "Could not parse overall score key {Key} from response, using weighted average: {Score}",
                overallScoreKey, overallScore);
        }

        // Extract verdict
        var verdictMatch = Regex.Match(response, @"VERDICT:\s*(.+?)(?:\r?\n|$)", RegexOptions.IgnoreCase);
        var verdict = verdictMatch.Success ? verdictMatch.Groups[1].Value.Trim() : string.Empty;

        // Extract summary
        var summaryMatch = Regex.Match(response, @"SUMMARY:\s*(.+?)(?:\r?\n\r?\n|$)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var summary = summaryMatch.Success ? summaryMatch.Groups[1].Value.Trim() : string.Empty;

        if (string.IsNullOrEmpty(verdict) && overallScore == 0)
        {
            _logger.LogWarning("Failed to parse regulatory score response — returning zero score");
        }

        return new ParsedScore(overallScore, verdict, summary, categoryScores);
    }

    private static int CalculateWeightedAverage(List<CategoryScoreDto> categoryScores)
    {
        var totalWeight = categoryScores.Sum(c => c.Weight);
        if (totalWeight == 0) return 0;

        var weightedSum = categoryScores.Sum(c => c.Score * c.Weight);
        return (int)Math.Round(weightedSum / totalWeight);
    }

    #endregion

    #region Helpers

    private record CategoryWeight(string Key, string Label, decimal Weight);

    private List<CategoryWeight> ParseCategoryWeights(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<CategoryWeight>();

        try
        {
            var weights = JsonSerializer.Deserialize<List<CategoryWeight>>(json, CamelCaseOptions);
            return weights ?? new List<CategoryWeight>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse CategoryWeightsJson");
            return new List<CategoryWeight>();
        }
    }

    private static string BuildCategoriesBlock(List<CategoryWeight> weights)
    {
        var sb = new StringBuilder();
        foreach (var w in weights)
        {
            sb.AppendLine($"- {w.Key} ({w.Label}, weight {w.Weight:F1})");
        }
        return sb.ToString();
    }

    private static string BuildCriteriaBlock(List<RegulatoryCriteria>? criteria)
    {
        if (criteria == null || criteria.Count == 0)
            return "";

        var sb = new StringBuilder();
        sb.AppendLine("REGULATORY CRITERIA TO ASSESS:");
        foreach (var c in criteria.OrderBy(c => c.CategoryKey).ThenBy(c => c.DisplayOrder))
        {
            sb.AppendLine($"- [{c.CategoryKey}] {c.CriteriaText}");
        }
        return sb.ToString();
    }

    private RegulatoryScoreResultDto MapToDto(
        ValidationRegulatoryScore score,
        RegulatoryProfile? profile,
        List<ValidationRegulatoryScore> allScores)
    {
        var categoryScores = new List<CategoryScoreDto>();
        try
        {
            categoryScores = JsonSerializer.Deserialize<List<CategoryScoreDto>>(
                score.CategoryScoresJson, CamelCaseOptions) ?? new();
        }
        catch
        {
            // Ignore deserialization failures
        }

        int? comparisonDelta = null;
        var previous = allScores
            .Where(s => s.ScoreType == score.ScoreType && s.RunNumber == score.RunNumber - 1)
            .FirstOrDefault();
        if (previous != null)
            comparisonDelta = score.OverallScore - previous.OverallScore;

        return new RegulatoryScoreResultDto
        {
            Id = score.Id,
            ScoreType = score.ScoreType,
            OverallScore = score.OverallScore,
            Verdict = score.Verdict,
            Summary = score.Summary,
            CategoryScores = categoryScores,
            RunLabel = score.RunLabel,
            RunNumber = score.RunNumber,
            RegulatoryBody = score.RegulatoryBody,
            ScoreLabel = profile?.ScoreLabel,
            ScoredSectionCount = score.ScoredSectionCount,
            TargetLanguage = score.TargetLanguage,
            CreatedAt = score.CreatedAt,
            ComparisonDelta = comparisonDelta,
            FullResponse = score.FullResponseJson
        };
    }

    #endregion
}
