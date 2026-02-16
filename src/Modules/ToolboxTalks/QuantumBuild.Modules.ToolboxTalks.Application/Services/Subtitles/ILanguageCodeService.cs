namespace QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;

/// <summary>
/// Service for mapping between language names and ISO 639-1 language codes.
/// Used for generating localized SRT file names and API calls.
/// </summary>
public interface ILanguageCodeService
{
    /// <summary>
    /// Gets the ISO 639-1 language code for a language name.
    /// </summary>
    /// <param name="languageName">Language name (e.g., "Spanish", "Polish")</param>
    /// <returns>Language code (e.g., "es", "pl")</returns>
    string GetLanguageCode(string languageName);

    /// <summary>
    /// Gets the display name for a language code.
    /// </summary>
    /// <param name="languageCode">ISO 639-1 code (e.g., "es")</param>
    /// <returns>Language name (e.g., "Spanish")</returns>
    string GetLanguageName(string languageCode);

    /// <summary>
    /// Checks if a language name is valid and supported.
    /// </summary>
    /// <param name="languageName">Language name to validate</param>
    /// <returns>True if the language is supported</returns>
    bool IsValidLanguage(string languageName);

    /// <summary>
    /// Gets all supported languages with their codes.
    /// </summary>
    /// <returns>Dictionary of language names to codes</returns>
    IReadOnlyDictionary<string, string> GetAllLanguages();
}
