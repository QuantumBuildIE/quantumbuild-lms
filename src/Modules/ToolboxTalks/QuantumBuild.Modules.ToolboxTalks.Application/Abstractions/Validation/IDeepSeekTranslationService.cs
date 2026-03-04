namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Back-translation service using the DeepSeek API (OpenAI-compatible format).
/// Translates text from a target language back to the source language for validation.
/// </summary>
public interface IDeepSeekTranslationService
{
    /// <summary>
    /// Translates text from the target language back to the source language using DeepSeek.
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
