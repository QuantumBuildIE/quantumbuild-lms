namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Back-translation service using Claude Sonnet (claude-sonnet-4-20250514) via the Anthropic Messages API.
/// Provider D in the consensus engine — Round 3 final tiebreaker, fires only when Rounds 1 and 2 disagree.
/// Replaced DeepSeek in pipeline v6.4 for GDPR compliance (DeepSeek has indefinite data retention
/// on China-based servers).
/// </summary>
public interface IClaudeSonnetBackTranslationService
{
    /// <summary>
    /// Translates text from the target language back to the source language using Claude Sonnet.
    /// Returns null if the provider is unavailable (no API key configured).
    /// </summary>
    Task<BackTranslationResult?> BackTranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default,
        Guid tenantId = default,
        Guid? userId = null,
        bool isSystemCall = true,
        Guid? toolboxTalkId = null);
}
