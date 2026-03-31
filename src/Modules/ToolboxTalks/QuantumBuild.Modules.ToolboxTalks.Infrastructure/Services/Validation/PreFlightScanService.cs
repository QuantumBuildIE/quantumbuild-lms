using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.PreFlightScan;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

public class PreFlightScanService(
    HttpClient httpClient,
    IOptions<SubtitleProcessingSettings> settings,
    IAiUsageLogger aiUsageLogger,
    ILogger<PreFlightScanService> logger) : IPreFlightScanService
{
    private const string HaikuModel = "claude-haiku-4-5-20251001";
    private const int MaxTokens = 4096;

    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<PreFlightScanResult> ScanAsync(
        IReadOnlyList<string> sectionTexts,
        string targetLanguage,
        string? sectorKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var combinedText = string.Join("\n\n---\n\n", sectionTexts);
            var prompt = BuildPrompt(combinedText, targetLanguage, sectorKey);
            var responseText = await CallClaudeAsync(prompt, cancellationToken);
            var findings = ParseResponse(responseText);

            return new PreFlightScanResult(
                findings,
                HasFindings: findings.Count > 0,
                HighRiskCount: findings.Count(f => f.Type == PreFlightFindingType.HighRiskTerm),
                ProperNounCount: findings.Count(f => f.Type == PreFlightFindingType.ProperNoun),
                RoleConstructCount: findings.Count(f => f.Type == PreFlightFindingType.RoleConstruct));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pre-flight scan failed for {TargetLanguage}, sector {SectorKey}", targetLanguage, sectorKey);
            return new PreFlightScanResult([], HasFindings: false, HighRiskCount: 0, ProperNounCount: 0, RoleConstructCount: 0);
        }
    }

    private static string BuildPrompt(string sourceText, string targetLanguage, string? sectorKey)
    {
        var sectorContext = string.IsNullOrWhiteSpace(sectorKey)
            ? ""
            : $"\nThe content is from the {sectorKey} sector — prioritise sector-specific terminology.\n";

        return $$"""
            You are a translation pre-flight analyst. Analyse the following source text that will be translated into {{targetLanguage}}.{{sectorContext}}
            Identify potential translation risks in these categories:

            1. **highRiskTerms** — terms with known mistranslation risk in {{targetLanguage}}. Include a "risk" explanation and a "suggestedTranslation" in {{targetLanguage}}.
            2. **properNouns** — system names, product names, UI element names, or brand names that should NOT be translated. Return as plain strings.
            3. **roleConstructs** — job titles or role references (e.g. "Site Safety Officer", "Line Manager") that need consistent translation. Include a "suggestedTranslation" in {{targetLanguage}}.
            4. **slashConstructs** — "X / Y" patterns where the slash separates alternatives or dual meanings that risk meaning change during translation. Include a "risk" explanation.

            Return ONLY valid JSON in exactly this shape — no markdown, no commentary:
            {
              "highRiskTerms": [
                {"term": "...", "risk": "...", "suggestedTranslation": "..."}
              ],
              "properNouns": ["..."],
              "roleConstructs": [
                {"term": "...", "suggestedTranslation": "..."}
              ],
              "slashConstructs": [
                {"term": "...", "risk": "..."}
              ]
            }

            If a category has no findings, return an empty array for it.

            SOURCE TEXT:
            {{sourceText}}
            """;
    }

    private async Task<string> CallClaudeAsync(string prompt, CancellationToken cancellationToken)
    {
        var claudeSettings = settings.Value.Claude;

        if (string.IsNullOrWhiteSpace(claudeSettings.ApiKey))
            throw new InvalidOperationException("Claude API key not configured");

        var requestBody = new
        {
            model = HaikuModel,
            max_tokens = MaxTokens,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{claudeSettings.BaseUrl}/messages");
        request.Headers.Add("x-api-key", claudeSettings.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Claude pre-flight scan failed: {StatusCode} - {Response}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Claude API error: {response.StatusCode}");
        }

        var parsed = AnthropicResponseParser.Parse(responseBody);

        await aiUsageLogger.LogAsync(
            Guid.Empty,
            AiOperationCategory.DialectDetection,
            parsed.Model,
            parsed.InputTokens,
            parsed.OutputTokens,
            isSystemCall: false,
            userId: null,
            referenceEntityId: null,
            cancellationToken);

        return parsed.ContentText;
    }

    private static IReadOnlyList<PreFlightFinding> ParseResponse(string responseText)
    {
        var json = responseText.Trim();

        // Strip markdown code fences if present
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline >= 0) json = json[(firstNewline + 1)..];
            if (json.EndsWith("```")) json = json[..^3];
            json = json.Trim();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var findings = new List<PreFlightFinding>();

        if (root.TryGetProperty("highRiskTerms", out var highRisk))
        {
            foreach (var item in highRisk.EnumerateArray())
            {
                findings.Add(new PreFlightFinding(
                    PreFlightFindingType.HighRiskTerm,
                    item.GetProperty("term").GetString() ?? "",
                    item.GetProperty("risk").GetString() ?? "",
                    item.TryGetProperty("suggestedTranslation", out var st) ? st.GetString() : null));
            }
        }

        if (root.TryGetProperty("properNouns", out var properNouns))
        {
            foreach (var item in properNouns.EnumerateArray())
            {
                findings.Add(new PreFlightFinding(
                    PreFlightFindingType.ProperNoun,
                    item.GetString() ?? "",
                    "Should not be translated",
                    SuggestedTranslation: null));
            }
        }

        if (root.TryGetProperty("roleConstructs", out var roles))
        {
            foreach (var item in roles.EnumerateArray())
            {
                findings.Add(new PreFlightFinding(
                    PreFlightFindingType.RoleConstruct,
                    item.GetProperty("term").GetString() ?? "",
                    "Job title requiring consistent translation",
                    item.TryGetProperty("suggestedTranslation", out var st) ? st.GetString() : null));
            }
        }

        if (root.TryGetProperty("slashConstructs", out var slashes))
        {
            foreach (var item in slashes.EnumerateArray())
            {
                findings.Add(new PreFlightFinding(
                    PreFlightFindingType.SlashConstruct,
                    item.GetProperty("term").GetString() ?? "",
                    item.GetProperty("risk").GetString() ?? "",
                    SuggestedTranslation: null));
            }
        }

        return findings;
    }
}
