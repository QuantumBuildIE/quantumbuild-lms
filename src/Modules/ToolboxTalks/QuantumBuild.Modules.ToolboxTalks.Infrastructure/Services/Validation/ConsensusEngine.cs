using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

// Round 1A: Claude Haiku  (claude-haiku-4-5-20251001) — back-translation A
// Round 1B: DeepL         — back-translation B (independent signal)
// Round 2:  Gemini        — tiebreaker (different training lineage)
// Round 3:  Claude Sonnet (claude-sonnet-4-20250514) — final tiebreaker,
//           fires only when rounds 1 and 2 disagree
// Pipeline v6.4: DeepSeek removed — GDPR risk (indefinite retention, China-based servers)
public class ConsensusEngine : IConsensusEngine
{
    /// <summary>
    /// Minimum agreement percentage between providers to consider consensus strong.
    /// If both provider scores are above threshold AND agreement is above this value, Round 1 passes.
    /// </summary>
    private const int AgreementThreshold = 10; // Max score difference between A and B for high agreement

    private readonly IClaudeHaikuBackTranslationService _claudeHaiku;
    private readonly IDeepLTranslationService _deepL;
    private readonly IGeminiTranslationService _gemini;
    private readonly IClaudeSonnetBackTranslationService _claudeSonnet;
    private readonly ILexicalScoringService _scorer;
    private readonly TranslationValidationSettings _settings;
    private readonly ILogger<ConsensusEngine> _logger;

    public ConsensusEngine(
        IClaudeHaikuBackTranslationService claudeHaiku,
        IDeepLTranslationService deepL,
        IGeminiTranslationService gemini,
        IClaudeSonnetBackTranslationService claudeSonnet,
        ILexicalScoringService scorer,
        IOptions<TranslationValidationSettings> settings,
        ILogger<ConsensusEngine> logger)
    {
        _claudeHaiku = claudeHaiku;
        _deepL = deepL;
        _gemini = gemini;
        _claudeSonnet = claudeSonnet;
        _scorer = scorer;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ConsensusResult> RunAsync(
        string originalText,
        string translatedText,
        string sourceLanguage,
        string targetLanguage,
        int threshold,
        CancellationToken cancellationToken = default,
        Guid tenantId = default,
        Guid? toolboxTalkId = null)
    {
        var result = new ConsensusResult();
        var maxRounds = _settings.MaxRounds;

        _logger.LogInformation(
            "ConsensusEngine starting. Threshold: {Threshold}, MaxRounds: {MaxRounds}",
            threshold, maxRounds);

        // ── Round 1: Claude Haiku (A) + DeepL (B) ──
        result.RoundsUsed = 1;

        var resultA = await _claudeHaiku.BackTranslateAsync(
            translatedText, sourceLanguage, targetLanguage, cancellationToken,
            tenantId: tenantId, toolboxTalkId: toolboxTalkId);
        var resultB = await _deepL.BackTranslateAsync(
            translatedText, sourceLanguage, targetLanguage, cancellationToken);

        ApplyBackTranslation(result, 'A', resultA, originalText);
        ApplyBackTranslation(result, 'B', resultB, originalText);

        _logger.LogInformation(
            "Round 1 complete. ScoreA={ScoreA} ({ProvA}), ScoreB={ScoreB} ({ProvB})",
            result.ScoreA, resultA?.ProviderName ?? "unavailable",
            result.ScoreB, resultB?.ProviderName ?? "unavailable");

        if (EvaluateRound1(result, threshold))
        {
            result.Outcome = ValidationOutcome.Pass;
            result.FinalScore = CalculateFinalScore(result);
            _logger.LogInformation(
                "Round 1 PASS. FinalScore={FinalScore}", result.FinalScore);
            return result;
        }

        // ── Round 2: Add Gemini (C) if configured ──
        if (maxRounds >= 2)
        {
            result.RoundsUsed = 2;

            var resultC = await _gemini.BackTranslateAsync(
                translatedText, sourceLanguage, targetLanguage, cancellationToken);

            if (resultC != null)
            {
                ApplyBackTranslation(result, 'C', resultC, originalText);

                _logger.LogInformation(
                    "Round 2 complete. ScoreC={ScoreC} ({Prov})",
                    result.ScoreC, resultC.ProviderName);

                var finalScore = CalculateFinalScore(result);
                if (finalScore >= threshold)
                {
                    result.Outcome = ValidationOutcome.Pass;
                    result.FinalScore = finalScore;
                    _logger.LogInformation(
                        "Round 2 PASS. FinalScore={FinalScore}", result.FinalScore);
                    return result;
                }
            }
            else
            {
                _logger.LogInformation("Round 2 skipped — Gemini provider unavailable");
            }
        }

        // ── Round 3: Claude Sonnet (D) — final tiebreaker ──
        if (maxRounds >= 3)
        {
            result.RoundsUsed = 3;

            var resultD = await _claudeSonnet.BackTranslateAsync(
                translatedText, sourceLanguage, targetLanguage, cancellationToken,
                tenantId: tenantId, toolboxTalkId: toolboxTalkId);

            if (resultD != null)
            {
                ApplyBackTranslation(result, 'D', resultD, originalText);

                _logger.LogInformation(
                    "Round 3 complete. ScoreD={ScoreD} ({Prov})",
                    result.ScoreD, resultD.ProviderName);
            }
            else
            {
                _logger.LogInformation("Round 3 — Claude Sonnet provider unavailable");
            }
        }

        // ── Final determination ──
        result.FinalScore = CalculateFinalScore(result);
        result.Outcome = DetermineOutcome(result.FinalScore, threshold);

        _logger.LogInformation(
            "Consensus complete after {Rounds} round(s). FinalScore={FinalScore}, Outcome={Outcome}",
            result.RoundsUsed, result.FinalScore, result.Outcome);

        return result;
    }

    /// <summary>
    /// Applies a back-translation result to the appropriate slot and scores it.
    /// </summary>
    private void ApplyBackTranslation(ConsensusResult result, char slot, BackTranslationResult? btResult, string originalText)
    {
        if (btResult == null)
        {
            _logger.LogWarning("Provider {Slot} returned null (not configured or skipped)", slot);
            return;
        }

        if (!btResult.Success)
        {
            _logger.LogWarning(
                "Provider {Slot} ({Provider}) failed: {Error}",
                slot, btResult.ProviderName, btResult.ErrorMessage);
            return;
        }

        var score = (int)Math.Round(_scorer.Score(originalText, btResult.BackTranslatedText));

        switch (slot)
        {
            case 'A':
                result.BackTranslationA = btResult.BackTranslatedText;
                result.ScoreA = score;
                break;
            case 'B':
                result.BackTranslationB = btResult.BackTranslatedText;
                result.ScoreB = score;
                break;
            case 'C':
                result.BackTranslationC = btResult.BackTranslatedText;
                result.ScoreC = score;
                break;
            case 'D':
                result.BackTranslationD = btResult.BackTranslatedText;
                result.ScoreD = score;
                break;
        }
    }

    /// <summary>
    /// Evaluates Round 1: both A and B must exceed threshold AND agreement must be high.
    /// </summary>
    private static bool EvaluateRound1(ConsensusResult result, int threshold)
    {
        // Both providers must have produced a result
        if (result.BackTranslationA == null || result.BackTranslationB == null)
            return false;

        // Both scores must exceed threshold
        if (result.ScoreA < threshold || result.ScoreB < threshold)
            return false;

        // Agreement between A and B must be within the tolerance
        var scoreDifference = Math.Abs(result.ScoreA - result.ScoreB);
        return scoreDifference <= AgreementThreshold;
    }

    /// <summary>
    /// Calculates the final aggregated score as the average of all available scores.
    /// </summary>
    private static int CalculateFinalScore(ConsensusResult result)
    {
        var scores = new List<int>();

        if (result.BackTranslationA != null) scores.Add(result.ScoreA);
        if (result.BackTranslationB != null) scores.Add(result.ScoreB);
        if (result.ScoreC.HasValue) scores.Add(result.ScoreC.Value);
        if (result.ScoreD.HasValue) scores.Add(result.ScoreD.Value);

        return scores.Count > 0
            ? (int)Math.Round(scores.Average())
            : 0;
    }

    /// <summary>
    /// Determines the outcome based on final score relative to threshold.
    /// Fail if below threshold - 15, Review if between, Pass if at or above threshold.
    /// </summary>
    private static ValidationOutcome DetermineOutcome(int finalScore, int threshold)
    {
        if (finalScore >= threshold)
            return ValidationOutcome.Pass;

        // If within 15 points of threshold, mark for review instead of hard fail
        if (finalScore >= threshold - 15)
            return ValidationOutcome.Review;

        return ValidationOutcome.Fail;
    }
}
