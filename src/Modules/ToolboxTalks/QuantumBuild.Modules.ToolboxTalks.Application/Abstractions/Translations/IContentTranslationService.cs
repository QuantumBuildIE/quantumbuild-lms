namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;

/// <summary>
/// Service for translating general content (text, HTML) using AI translation.
/// Different from ITranslationService which is specific to SRT subtitles.
/// </summary>
public interface IContentTranslationService
{
    /// <summary>
    /// Translates plain text or HTML content to the target language.
    /// </summary>
    /// <param name="text">The text to translate</param>
    /// <param name="targetLanguage">Target language name (e.g., "Polish", "Romanian")</param>
    /// <param name="isHtml">If true, preserves HTML tags while translating text content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="sourceLanguage">Source language name (e.g., "English", "Afrikaans"). Defaults to "English" if null.</param>
    /// <returns>Translation result with translated content</returns>
    Task<ContentTranslationResult> TranslateTextAsync(
        string text,
        string targetLanguage,
        bool isHtml = false,
        CancellationToken cancellationToken = default,
        string? sourceLanguage = null);

    /// <summary>
    /// Sends a custom prompt directly to the AI without wrapping it in translation instructions.
    /// Use this when you've already constructed the full prompt (e.g., slideshow JSON translation).
    /// </summary>
    /// <param name="prompt">The complete prompt to send to the AI</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Translation result with the AI response</returns>
    Task<ContentTranslationResult> SendCustomPromptAsync(
        string prompt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Translates multiple items in a batch for efficiency.
    /// </summary>
    /// <param name="items">Items to translate with their context</param>
    /// <param name="targetLanguage">Target language name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="sourceLanguage">Source language name. Defaults to "English" if null.</param>
    /// <returns>Translation results keyed by item index</returns>
    Task<BatchTranslationResult> TranslateBatchAsync(
        IEnumerable<TranslationItem> items,
        string targetLanguage,
        CancellationToken cancellationToken = default,
        string? sourceLanguage = null);
}

/// <summary>
/// Represents an item to be translated in a batch.
/// </summary>
public class TranslationItem
{
    /// <summary>
    /// Unique key to identify this item in results
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The text content to translate
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Whether the text contains HTML that should be preserved
    /// </summary>
    public bool IsHtml { get; set; }

    /// <summary>
    /// Optional context hint (e.g., "section title", "quiz question")
    /// </summary>
    public string? Context { get; set; }
}

/// <summary>
/// Result of a single content translation operation.
/// </summary>
public class ContentTranslationResult
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
    /// The translated content
    /// </summary>
    public string TranslatedContent { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static ContentTranslationResult SuccessResult(string translatedContent) =>
        new()
        {
            Success = true,
            TranslatedContent = translatedContent
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static ContentTranslationResult FailureResult(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Result of a batch translation operation.
/// </summary>
public class BatchTranslationResult
{
    /// <summary>
    /// Whether the overall batch was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the batch failed entirely
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Individual translation results keyed by the item key
    /// </summary>
    public Dictionary<string, ContentTranslationResult> Results { get; set; } = new();

    /// <summary>
    /// Creates a successful batch result.
    /// </summary>
    public static BatchTranslationResult SuccessResult(Dictionary<string, ContentTranslationResult> results) =>
        new()
        {
            Success = true,
            Results = results
        };

    /// <summary>
    /// Creates a failed batch result.
    /// </summary>
    public static BatchTranslationResult FailureResult(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
