using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Result of the multi-round consensus back-translation scoring process.
/// </summary>
public class ConsensusResult
{
    /// <summary>
    /// Back-translation from Provider A (Claude Haiku).
    /// </summary>
    public string? BackTranslationA { get; set; }

    /// <summary>
    /// Back-translation from Provider B (DeepL).
    /// </summary>
    public string? BackTranslationB { get; set; }

    /// <summary>
    /// Back-translation from Provider C (Gemini).
    /// </summary>
    public string? BackTranslationC { get; set; }

    /// <summary>
    /// Back-translation from Provider D (DeepSeek).
    /// </summary>
    public string? BackTranslationD { get; set; }

    /// <summary>
    /// Lexical score for Provider A back-translation vs original.
    /// </summary>
    public int ScoreA { get; set; }

    /// <summary>
    /// Lexical score for Provider B back-translation vs original.
    /// </summary>
    public int ScoreB { get; set; }

    /// <summary>
    /// Lexical score for Provider C back-translation vs original (null if not used).
    /// </summary>
    public int? ScoreC { get; set; }

    /// <summary>
    /// Lexical score for Provider D back-translation vs original (null if not used).
    /// </summary>
    public int? ScoreD { get; set; }

    /// <summary>
    /// Number of consensus rounds used (1-3).
    /// </summary>
    public int RoundsUsed { get; set; }

    /// <summary>
    /// Final aggregated score across all providers.
    /// </summary>
    public int FinalScore { get; set; }

    /// <summary>
    /// Final validation outcome (Pass, Review, or Fail).
    /// </summary>
    public ValidationOutcome Outcome { get; set; }
}

/// <summary>
/// Implements escalating multi-round back-translation consensus logic.
/// Round 1: Claude Haiku (A) + DeepL (B). If both pass threshold and agreement is high → PASS.
/// Round 2: Add Gemini (C) if configured. Recalculate.
/// Round 3: Add DeepSeek (D) if configured. Final determination.
/// </summary>
public interface IConsensusEngine
{
    /// <summary>
    /// Runs the escalating consensus rounds on the given translated text.
    /// </summary>
    /// <param name="originalText">The original source-language text</param>
    /// <param name="translatedText">The translated text to validate</param>
    /// <param name="sourceLanguage">Source language code (e.g., "en")</param>
    /// <param name="targetLanguage">Target language code (e.g., "pl")</param>
    /// <param name="threshold">Pass threshold (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Consensus result with all back-translations, scores, and final outcome</returns>
    Task<ConsensusResult> RunAsync(
        string originalText,
        string translatedText,
        string sourceLanguage,
        string targetLanguage,
        int threshold,
        CancellationToken cancellationToken = default,
        Guid tenantId = default,
        Guid? toolboxTalkId = null);
}
