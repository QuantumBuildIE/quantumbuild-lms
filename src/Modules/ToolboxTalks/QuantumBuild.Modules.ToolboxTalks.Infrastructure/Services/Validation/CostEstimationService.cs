using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Estimates EUR cost of a corpus run using static per-provider rate tables (April 2026).
/// </summary>
public class CostEstimationService : ICostEstimationService
{
    // ── Static rate table (EUR, April 2026) ────────────────────────────────────

    // claude-haiku-4-5-20251001
    private const decimal HaikuInputPer1K = 0.00074m;
    private const decimal HaikuOutputPer1K = 0.00370m;

    // claude-sonnet-4-20250514
    private const decimal SonnetInputPer1K = 0.00277m;
    private const decimal SonnetOutputPer1K = 0.01385m;

    // gemini-2.0-flash
    private const decimal GeminiInputPer1K = 0.00007m;
    private const decimal GeminiOutputPer1K = 0.00028m;

    // deepl — per character
    private const decimal DeepLPerChar = 0.00002m;

    // Fraction of entries expected to reach Round 3
    private const decimal Round3EstimatedFraction = 0.30m;

    // ───────────────────────────────────────────────────────────────────────────

    public decimal EstimateCorpusRunCostEur(
        IEnumerable<AuditCorpusEntry> entries,
        int maxRounds,
        bool isSmokeTest)
    {
        var entryList = entries.ToList();
        if (entryList.Count == 0) return 0m;

        // Smoke test: cap at 5 entries
        var effectiveCount = isSmokeTest
            ? Math.Min(5, entryList.Count)
            : entryList.Count;

        var entriesToEstimate = entryList.Take(effectiveCount).ToList();

        var total = 0m;

        foreach (var entry in entriesToEstimate)
        {
            // Token approximation: 1 token ≈ 3.5 characters
            var inputTokens = (entry.OriginalText.Length + entry.TranslatedText.Length) / 3.5m;
            var outputTokens = entry.TranslatedText.Length / 3.5m;

            // Round 1A — Claude Haiku back-translation (always runs)
            total += (inputTokens / 1000m) * HaikuInputPer1K
                   + (outputTokens / 1000m) * HaikuOutputPer1K;

            // Round 1B — DeepL back-translation (always runs, character-based)
            total += entry.TranslatedText.Length * DeepLPerChar;

            // Round 2C — Gemini (if maxRounds >= 2)
            if (maxRounds >= 2)
            {
                total += (inputTokens / 1000m) * GeminiInputPer1K
                       + (outputTokens / 1000m) * GeminiOutputPer1K;
            }

            // Round 3D — Claude Sonnet (30% of entries estimated to reach Round 3)
            if (maxRounds >= 3)
            {
                total += Round3EstimatedFraction * (
                    (inputTokens / 1000m) * SonnetInputPer1K
                  + (outputTokens / 1000m) * SonnetOutputPer1K);
            }
        }

        return Math.Round(total, 4);
    }
}
