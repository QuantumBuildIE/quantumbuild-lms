using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

public partial class SafetyClassificationService(
    IToolboxTalksDbContext dbContext,
    ILogger<SafetyClassificationService> logger) : ISafetyClassificationService
{
    // Scoped cache: sector key → glossary terms (avoids repeated DB hits within same request)
    private readonly Dictionary<string, List<SafetyGlossaryTerm>> _glossaryCache = new();

    // Regex patterns for safety-critical sentence structures
    [GeneratedRegex(@"\b(do\s+not|don['']t|never|must\s+not|shall\s+not|prohibited|forbidden)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ProhibitionPattern();

    [GeneratedRegex(@"\b(in\s+the\s+event\s+of|evacuate|emergency|call\s+999|call\s+911|dial\s+112|first\s+aid|seek\s+medical)\b", RegexOptions.IgnoreCase)]
    private static partial Regex EmergencyPattern();

    [GeneratedRegex(@"\b(danger|warning|caution|hazard|hazardous|toxic|flammable|explosive|corrosive|lethal|fatal)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HazardPattern();

    public async Task<SafetyClassificationResult> ClassifyAsync(
        string text,
        string sectorKey,
        string targetLanguageCode,
        CancellationToken cancellationToken = default)
    {
        var criticalTermsFound = new List<string>();
        var glossaryMatches = new List<GlossaryMatch>();

        // 1. Load glossary terms for the sector (cached per request scope)
        var terms = await GetGlossaryTermsAsync(sectorKey, cancellationToken);

        // 2. Scan text for glossary terms
        foreach (var term in terms)
        {
            if (text.Contains(term.EnglishTerm, StringComparison.OrdinalIgnoreCase))
            {
                if (term.IsCritical)
                    criticalTermsFound.Add(term.EnglishTerm);

                var expectedTranslation = GetTranslation(term.Translations, targetLanguageCode);

                glossaryMatches.Add(new GlossaryMatch(
                    term.EnglishTerm,
                    term.Category,
                    expectedTranslation));
            }
        }

        // 3. Scan for safety-critical sentence structures via regex
        ScanRegexPatterns(text, "prohibition", ProhibitionPattern(), criticalTermsFound);
        ScanRegexPatterns(text, "emergency", EmergencyPattern(), criticalTermsFound);
        ScanRegexPatterns(text, "hazard", HazardPattern(), criticalTermsFound);

        var isSafetyCritical = criticalTermsFound.Count > 0;

        logger.LogDebug(
            "Safety classification for sector '{SectorKey}': IsCritical={IsCritical}, " +
            "CriticalTerms={CriticalTermCount}, GlossaryMatches={GlossaryMatchCount}",
            sectorKey, isSafetyCritical, criticalTermsFound.Count, glossaryMatches.Count);

        return new SafetyClassificationResult(
            isSafetyCritical,
            criticalTermsFound.Distinct().ToList(),
            glossaryMatches);
    }

    private async Task<List<SafetyGlossaryTerm>> GetGlossaryTermsAsync(
        string sectorKey,
        CancellationToken cancellationToken)
    {
        if (_glossaryCache.TryGetValue(sectorKey, out var cached))
            return cached;

        // Load active glossary for this sector (system-wide where TenantId is null,
        // or tenant-specific — EF query filter handles tenant scoping)
        var terms = await dbContext.SafetyGlossaryTerms
            .AsNoTracking()
            .Where(t => t.Glossary.SectorKey == sectorKey && t.Glossary.IsActive)
            .ToListAsync(cancellationToken);

        _glossaryCache[sectorKey] = terms;
        return terms;
    }

    private static string? GetTranslation(string translationsJson, string languageCode)
    {
        if (string.IsNullOrWhiteSpace(translationsJson))
            return null;

        try
        {
            var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(translationsJson);
            return translations?.GetValueOrDefault(languageCode);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ScanRegexPatterns(
        string text,
        string category,
        Regex pattern,
        List<string> criticalTermsFound)
    {
        foreach (Match match in pattern.Matches(text))
        {
            criticalTermsFound.Add($"[{category}] {match.Value}");
        }
    }
}
