namespace QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;

/// <summary>
/// Service for mapping between language names and ISO 639-1 language codes.
/// Provides a comprehensive list of commonly used languages for subtitle translation.
/// </summary>
public class LanguageCodeService : ILanguageCodeService
{
    private static readonly Dictionary<string, string> LanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Afrikaans", "af" },
        { "Arabic", "ar" },
        { "Bulgarian", "bg" },
        { "Chinese", "zh" },
        { "Croatian", "hr" },
        { "Czech", "cs" },
        { "Danish", "da" },
        { "Dutch", "nl" },
        { "English", "en" },
        { "Filipino", "fil" },
        { "Finnish", "fi" },
        { "French", "fr" },
        { "German", "de" },
        { "Greek", "el" },
        { "Hindi", "hi" },
        { "Hungarian", "hu" },
        { "Indonesian", "id" },
        { "Italian", "it" },
        { "Japanese", "ja" },
        { "Korean", "ko" },
        { "Latvian", "lv" },
        { "Lithuanian", "lt" },
        { "Malay", "ms" },
        { "Norwegian", "no" },
        { "Polish", "pl" },
        { "Portuguese", "pt" },
        { "Romanian", "ro" },
        { "Russian", "ru" },
        { "Slovak", "sk" },
        { "Spanish", "es" },
        { "Swedish", "sv" },
        { "Tamil", "ta" },
        { "Turkish", "tr" },
        { "Ukrainian", "uk" },
        { "Vietnamese", "vi" }
    };

    private static readonly Dictionary<string, string> CodeToName =
        LanguageCodes.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the ISO 639-1 language code for a language name.
    /// Falls back to first two characters of the language name if not found.
    /// </summary>
    public string GetLanguageCode(string languageName)
    {
        return LanguageCodes.TryGetValue(languageName, out var code)
            ? code
            : languageName.ToLowerInvariant()[..Math.Min(2, languageName.Length)];
    }

    /// <summary>
    /// Gets the display name for a language code.
    /// Returns the code itself if not found.
    /// </summary>
    public string GetLanguageName(string languageCode)
    {
        return CodeToName.TryGetValue(languageCode, out var name) ? name : languageCode;
    }

    /// <summary>
    /// Checks if a language name is valid and supported.
    /// </summary>
    public bool IsValidLanguage(string languageName)
    {
        return LanguageCodes.ContainsKey(languageName);
    }

    /// <summary>
    /// Gets all supported languages with their codes.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAllLanguages() => LanguageCodes;
}
