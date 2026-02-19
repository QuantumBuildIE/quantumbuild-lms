using QuantumBuild.Core.Application.Features.Lookups;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;

/// <summary>
/// Service for mapping between language names and ISO 639-1 language codes.
/// Reads from the Language lookup category in the database via ILookupService.
/// Caches the loaded languages for the lifetime of the scoped instance.
/// </summary>
public class LanguageCodeService : ILanguageCodeService
{
    private readonly ILookupService _lookupService;
    private readonly ICurrentUserService _currentUserService;

    private Dictionary<string, string>? _nameToCode;
    private Dictionary<string, string>? _codeToName;

    public LanguageCodeService(ILookupService lookupService, ICurrentUserService currentUserService)
    {
        _lookupService = lookupService;
        _currentUserService = currentUserService;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_nameToCode != null) return;

        _nameToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _codeToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var result = await _lookupService.GetEffectiveValuesAsync(
            _currentUserService.TenantId, "Language");

        if (result.Success && result.Data != null)
        {
            foreach (var value in result.Data)
            {
                _nameToCode[value.Name] = value.Code;
                _codeToName[value.Code] = value.Name;
            }
        }
    }

    /// <summary>
    /// Gets the ISO 639-1 language code for a language name.
    /// Falls back to first two characters of the language name if not found.
    /// </summary>
    public async Task<string> GetLanguageCodeAsync(string languageName)
    {
        await EnsureLoadedAsync();
        return _nameToCode!.TryGetValue(languageName, out var code)
            ? code
            : languageName.ToLowerInvariant()[..Math.Min(2, languageName.Length)];
    }

    /// <summary>
    /// Gets the display name for a language code.
    /// Returns the code itself if not found.
    /// </summary>
    public async Task<string> GetLanguageNameAsync(string languageCode)
    {
        await EnsureLoadedAsync();
        return _codeToName!.TryGetValue(languageCode, out var name) ? name : languageCode;
    }

    /// <summary>
    /// Checks if a language name is valid and supported.
    /// </summary>
    public async Task<bool> IsValidLanguageAsync(string languageName)
    {
        await EnsureLoadedAsync();
        return _nameToCode!.ContainsKey(languageName);
    }

    /// <summary>
    /// Gets all supported languages with their codes.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> GetAllLanguagesAsync()
    {
        await EnsureLoadedAsync();
        return _nameToCode!;
    }
}
