namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Back-translation service using Claude Haiku via the Anthropic Messages API.
/// Always available (uses the same API key as other Claude services).
/// Provider A in the consensus engine.
/// </summary>
public interface IClaudeHaikuBackTranslationService
{
    /// <summary>
    /// Translates text from the target language back to the source language using Claude Haiku.
    /// Returns null if the provider is unavailable (no API key configured).
    /// </summary>
    /// <param name="text">The translated text to back-translate</param>
    /// <param name="sourceLanguage">The original source language code (e.g., "en")</param>
    /// <param name="targetLanguage">The language the text is currently in (e.g., "pl")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Back-translation result, or null if provider is unavailable</returns>
    Task<BackTranslationResult?> BackTranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default);
}
