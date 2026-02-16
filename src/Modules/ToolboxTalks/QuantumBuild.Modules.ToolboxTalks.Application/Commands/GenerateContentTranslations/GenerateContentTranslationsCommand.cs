using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateContentTranslations;

/// <summary>
/// Command to generate content translations for a toolbox talk's sections and questions.
/// </summary>
public record GenerateContentTranslationsCommand : IRequest<GenerateContentTranslationsResult>
{
    /// <summary>
    /// ID of the toolbox talk to translate
    /// </summary>
    public Guid ToolboxTalkId { get; init; }

    /// <summary>
    /// Tenant ID for authorization
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Target languages to translate to (language names, e.g., "Polish", "Romanian")
    /// </summary>
    public List<string> TargetLanguages { get; init; } = new();
}

/// <summary>
/// Result of content translation generation
/// </summary>
public class GenerateContentTranslationsResult
{
    /// <summary>
    /// Whether the overall operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Results per language
    /// </summary>
    public List<LanguageTranslationResult> LanguageResults { get; set; } = new();

    public static GenerateContentTranslationsResult SuccessResult(List<LanguageTranslationResult> results) =>
        new()
        {
            Success = true,
            LanguageResults = results
        };

    public static GenerateContentTranslationsResult FailureResult(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Translation result for a single language
/// </summary>
public class LanguageTranslationResult
{
    /// <summary>
    /// Language name
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Language code (ISO 639-1)
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Whether this language translation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of sections translated
    /// </summary>
    public int SectionsTranslated { get; set; }

    /// <summary>
    /// Number of questions translated
    /// </summary>
    public int QuestionsTranslated { get; set; }

    /// <summary>
    /// Number of slides translated
    /// </summary>
    public int SlidesTranslated { get; set; }

    /// <summary>
    /// Whether the HTML slideshow was translated
    /// </summary>
    public bool SlideshowTranslated { get; set; }
}
