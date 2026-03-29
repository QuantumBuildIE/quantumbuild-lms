using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Back-translation service using the DeepL API.
/// Uses direct HTTP calls to the DeepL REST API with language code mapping.
/// </summary>
public class DeepLTranslationService : IDeepLTranslationService
{
    private const string ProviderName = "DeepL";

    private readonly HttpClient _httpClient;
    private readonly TranslationValidationSettings _settings;
    private readonly ILogger<DeepLTranslationService> _logger;

    /// <summary>
    /// Maps our internal language codes to DeepL source language codes.
    /// DeepL source codes are typically 2-letter ISO 639-1 (uppercase).
    /// </summary>
    private static readonly Dictionary<string, string> SourceLanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "EN",
        ["de"] = "DE",
        ["fr"] = "FR",
        ["es"] = "ES",
        ["it"] = "IT",
        ["nl"] = "NL",
        ["pl"] = "PL",
        ["pt"] = "PT",
        ["ru"] = "RU",
        ["ja"] = "JA",
        ["zh"] = "ZH",
        ["ko"] = "KO",
        ["cs"] = "CS",
        ["da"] = "DA",
        ["el"] = "EL",
        ["et"] = "ET",
        ["fi"] = "FI",
        ["hu"] = "HU",
        ["id"] = "ID",
        ["lv"] = "LV",
        ["lt"] = "LT",
        ["nb"] = "NB",
        ["ro"] = "RO",
        ["sk"] = "SK",
        ["sl"] = "SL",
        ["sv"] = "SV",
        ["tr"] = "TR",
        ["uk"] = "UK",
        ["bg"] = "BG",
        ["ar"] = "AR",
    };

    /// <summary>
    /// Maps our internal language codes to DeepL target language codes.
    /// DeepL target codes may require regional variants (e.g., EN-GB, PT-BR).
    /// </summary>
    private static readonly Dictionary<string, string> TargetLanguageMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "EN-GB",
        ["en-us"] = "EN-US",
        ["en-gb"] = "EN-GB",
        ["de"] = "DE",
        ["fr"] = "FR",
        ["es"] = "ES",
        ["it"] = "IT",
        ["nl"] = "NL",
        ["pl"] = "PL",
        ["pt"] = "PT-PT",
        ["pt-br"] = "PT-BR",
        ["ru"] = "RU",
        ["ja"] = "JA",
        ["zh"] = "ZH-HANS",
        ["zh-tw"] = "ZH-HANT",
        ["ko"] = "KO",
        ["cs"] = "CS",
        ["da"] = "DA",
        ["el"] = "EL",
        ["et"] = "ET",
        ["fi"] = "FI",
        ["hu"] = "HU",
        ["id"] = "ID",
        ["lv"] = "LV",
        ["lt"] = "LT",
        ["nb"] = "NB",
        ["ro"] = "RO",
        ["sk"] = "SK",
        ["sl"] = "SL",
        ["sv"] = "SV",
        ["tr"] = "TR",
        ["uk"] = "UK",
        ["bg"] = "BG",
        ["ar"] = "AR",
    };

    public DeepLTranslationService(
        HttpClient httpClient,
        IOptions<TranslationValidationSettings> settings,
        ILogger<DeepLTranslationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BackTranslationResult?> BackTranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.DeepL.ApiKey))
        {
            _logger.LogDebug("DeepL API key not configured, skipping provider");
            return null;
        }

        _logger.LogInformation(
            "DeepL configured with BaseUrl={BaseUrl}, ApiKey={KeyPrefix}***",
            _settings.DeepL.BaseUrl,
            _settings.DeepL.ApiKey.Length > 4 ? _settings.DeepL.ApiKey[..4] : "****");

        if (string.IsNullOrWhiteSpace(text))
        {
            return BackTranslationResult.SuccessResult(string.Empty, ProviderName);
        }

        // Back-translation: translate FROM targetLanguage TO sourceLanguage
        var deepLSource = MapSourceLanguage(targetLanguage);
        var deepLTarget = MapTargetLanguage(sourceLanguage);

        if (deepLSource == null || deepLTarget == null)
        {
            _logger.LogWarning(
                "DeepL does not support language pair {TargetLang} → {SourceLang}",
                targetLanguage, sourceLanguage);
            return BackTranslationResult.FailureResult(
                $"Unsupported language pair: {targetLanguage} → {sourceLanguage}", ProviderName);
        }

        try
        {
            _logger.LogInformation(
                "DeepL back-translation: {TargetLang} → {SourceLang}",
                targetLanguage, sourceLanguage);

            var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["text"] = text,
                ["source_lang"] = deepLSource,
                ["target_lang"] = deepLTarget
            });

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.DeepL.BaseUrl}/translate")
            {
                Content = formContent
            };
            request.Headers.Add("Authorization", $"DeepL-Auth-Key {_settings.DeepL.ApiKey}");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var hint = response.StatusCode == System.Net.HttpStatusCode.Forbidden
                    ? " (403 Forbidden — this usually means a paid API key is being used with the free-tier URL api-free.deepl.com, or vice versa. Check TranslationValidation__DeepL__BaseUrl: paid keys use https://api.deepl.com/v2, free keys use https://api-free.deepl.com/v2)"
                    : "";
                _logger.LogError(
                    "DeepL API error: {StatusCode} — BaseUrl={BaseUrl}, ResponseBody={Response}{Hint}",
                    response.StatusCode, _settings.DeepL.BaseUrl, responseBody, hint);
                return BackTranslationResult.FailureResult(
                    $"DeepL API error: {response.StatusCode}", ProviderName);
            }

            var translatedText = ParseDeepLResponse(responseBody);

            if (string.IsNullOrWhiteSpace(translatedText))
            {
                _logger.LogWarning("DeepL returned empty translation");
                return BackTranslationResult.FailureResult(
                    "DeepL returned empty translation", ProviderName);
            }

            _logger.LogInformation(
                "DeepL back-translation completed: {TargetLang} → {SourceLang}",
                targetLanguage, sourceLanguage);

            return BackTranslationResult.SuccessResult(translatedText, ProviderName);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "DeepL HTTP request failed");
            return BackTranslationResult.FailureResult(
                $"HTTP request failed: {ex.Message}", ProviderName);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse DeepL response");
            return BackTranslationResult.FailureResult(
                $"Failed to parse response: {ex.Message}", ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeepL back-translation failed");
            return BackTranslationResult.FailureResult(
                $"Back-translation failed: {ex.Message}", ProviderName);
        }
    }

    /// <summary>
    /// Maps an internal language code to a DeepL source language code.
    /// </summary>
    private static string? MapSourceLanguage(string languageCode)
    {
        if (SourceLanguageMap.TryGetValue(languageCode, out var mapped))
            return mapped;

        // Try the base language code (e.g., "en-gb" → "en")
        var baseLang = languageCode.Split('-')[0];
        if (SourceLanguageMap.TryGetValue(baseLang, out mapped))
            return mapped;

        // Fall back to uppercase of the base code (DeepL may support it)
        return baseLang.ToUpperInvariant();
    }

    /// <summary>
    /// Maps an internal language code to a DeepL target language code.
    /// </summary>
    private static string? MapTargetLanguage(string languageCode)
    {
        if (TargetLanguageMap.TryGetValue(languageCode, out var mapped))
            return mapped;

        // Try the base language code
        var baseLang = languageCode.Split('-')[0];
        if (TargetLanguageMap.TryGetValue(baseLang, out mapped))
            return mapped;

        // Fall back to uppercase of the base code
        return baseLang.ToUpperInvariant();
    }

    /// <summary>
    /// Parses the DeepL API response to extract translated text.
    /// </summary>
    private static string ParseDeepLResponse(string responseBody)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);

        if (!jsonDoc.RootElement.TryGetProperty("translations", out var translations))
            return string.Empty;

        foreach (var translation in translations.EnumerateArray())
        {
            if (translation.TryGetProperty("text", out var textEl))
            {
                return textEl.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
