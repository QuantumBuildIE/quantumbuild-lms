using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Back-translation service using the Google Gemini API.
/// Uses the generateContent endpoint with the configured model.
/// </summary>
public class GeminiTranslationService : IGeminiTranslationService
{
    private const string ProviderName = "Gemini";
    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    private readonly HttpClient _httpClient;
    private readonly TranslationValidationSettings _settings;
    private readonly ILogger<GeminiTranslationService> _logger;

    public GeminiTranslationService(
        HttpClient httpClient,
        IOptions<TranslationValidationSettings> settings,
        ILogger<GeminiTranslationService> logger)
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
        if (string.IsNullOrWhiteSpace(_settings.Gemini.ApiKey))
        {
            _logger.LogDebug("Gemini API key not configured, skipping provider");
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return BackTranslationResult.SuccessResult(string.Empty, ProviderName);
        }

        var delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Gemini back-translation attempt {Attempt}/{MaxRetries}: {TargetLang} → {SourceLang}",
                    attempt, MaxRetries, targetLanguage, sourceLanguage);

                var prompt = BuildBackTranslationPrompt(text, sourceLanguage, targetLanguage);

                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.1,
                        maxOutputTokens = 4096
                    }
                };

                var url = $"{_settings.Gemini.BaseUrl}/models/{_settings.Gemini.Model}:generateContent?key={_settings.Gemini.ApiKey}";

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        Encoding.UTF8,
                        "application/json")
                };

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < MaxRetries)
                {
                    _logger.LogWarning(
                        "Gemini rate limited on attempt {Attempt}, retrying in {Delay}s",
                        attempt, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                    continue;
                }

                if ((int)response.StatusCode >= 500 && attempt < MaxRetries)
                {
                    _logger.LogWarning(
                        "Gemini server error {StatusCode} on attempt {Attempt}, retrying in {Delay}s",
                        response.StatusCode, attempt, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Gemini API error: {StatusCode} - {Response}",
                        response.StatusCode, responseBody);
                    return BackTranslationResult.FailureResult(
                        $"Gemini API error: {response.StatusCode}", ProviderName);
                }

                var translatedText = ParseGeminiResponse(responseBody);

                if (string.IsNullOrWhiteSpace(translatedText))
                {
                    _logger.LogWarning("Gemini returned empty translation");
                    return BackTranslationResult.FailureResult(
                        "Gemini returned empty translation", ProviderName);
                }

                _logger.LogInformation(
                    "Gemini back-translation completed: {TargetLang} → {SourceLang}",
                    targetLanguage, sourceLanguage);

                return BackTranslationResult.SuccessResult(translatedText, ProviderName);
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(ex,
                    "Gemini HTTP error on attempt {Attempt}, retrying in {Delay}s",
                    attempt, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Gemini HTTP request failed after {MaxRetries} attempts", MaxRetries);
                return BackTranslationResult.FailureResult(
                    $"HTTP request failed: {ex.Message}", ProviderName);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini response");
                return BackTranslationResult.FailureResult(
                    $"Failed to parse response: {ex.Message}", ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini back-translation failed");
                return BackTranslationResult.FailureResult(
                    $"Back-translation failed: {ex.Message}", ProviderName);
            }
        }

        return BackTranslationResult.FailureResult(
            $"Failed after {MaxRetries} retry attempts", ProviderName);
    }

    /// <summary>
    /// Builds the back-translation prompt for Gemini.
    /// </summary>
    private static string BuildBackTranslationPrompt(string text, string sourceLanguage, string targetLanguage)
    {
        return $"""
            You are a professional translator. Translate the following text from {targetLanguage} back to {sourceLanguage}.

            IMPORTANT:
            - Provide ONLY the translated text, no explanations or notes.
            - Preserve the original meaning as accurately as possible.
            - Maintain the same tone and register.
            - Do not add or remove any content.

            Text to translate:
            {text}
            """;
    }

    /// <summary>
    /// Parses the Gemini API response to extract the generated text.
    /// Response format: { "candidates": [{ "content": { "parts": [{ "text": "..." }] } }] }
    /// </summary>
    private static string ParseGeminiResponse(string responseBody)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);

        if (!jsonDoc.RootElement.TryGetProperty("candidates", out var candidates))
            return string.Empty;

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (candidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textEl))
                    {
                        return textEl.GetString()?.Trim() ?? string.Empty;
                    }
                }
            }
        }

        return string.Empty;
    }
}
