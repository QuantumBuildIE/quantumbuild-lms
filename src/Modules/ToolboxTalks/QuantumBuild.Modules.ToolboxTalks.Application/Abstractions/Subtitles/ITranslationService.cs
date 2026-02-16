namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;

/// <summary>
/// Service for translating SRT subtitle content to different languages using AI (e.g., Claude).
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translates a batch of SRT content to the target language.
    /// </summary>
    /// <param name="srtContent">SRT formatted subtitle content to translate</param>
    /// <param name="targetLanguage">Target language name (e.g., "Spanish", "Polish")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translation result with translated SRT content</returns>
    Task<TranslationResult> TranslateSrtBatchAsync(
        string srtContent,
        string targetLanguage,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a translation operation
/// </summary>
public class TranslationResult
{
    /// <summary>
    /// Whether the translation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if translation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The translated SRT content
    /// </summary>
    public string TranslatedContent { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static TranslationResult SuccessResult(string translatedContent) =>
        new()
        {
            Success = true,
            TranslatedContent = translatedContent
        };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static TranslationResult FailureResult(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
