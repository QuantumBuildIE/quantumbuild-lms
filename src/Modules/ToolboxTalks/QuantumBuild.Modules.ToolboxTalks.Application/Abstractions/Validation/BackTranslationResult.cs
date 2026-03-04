namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Result of a back-translation operation from an external provider.
/// </summary>
public class BackTranslationResult
{
    /// <summary>
    /// Whether the back-translation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if back-translation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The back-translated text (target language → source language)
    /// </summary>
    public string BackTranslatedText { get; set; } = string.Empty;

    /// <summary>
    /// Name of the provider that performed the back-translation
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static BackTranslationResult SuccessResult(string backTranslatedText, string providerName) =>
        new()
        {
            Success = true,
            BackTranslatedText = backTranslatedText,
            ProviderName = providerName
        };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static BackTranslationResult FailureResult(string errorMessage, string providerName) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage,
            ProviderName = providerName
        };
}
