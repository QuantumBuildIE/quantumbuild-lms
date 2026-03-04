using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Back-translation service using the DeepSeek API (OpenAI-compatible format).
/// Base URL is configurable to support alternative OpenAI-compatible endpoints.
/// </summary>
public class DeepSeekTranslationService : IDeepSeekTranslationService
{
    private const string ProviderName = "DeepSeek";
    private const int MaxRetries = 3;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    private readonly HttpClient _httpClient;
    private readonly TranslationValidationSettings _settings;
    private readonly ILogger<DeepSeekTranslationService> _logger;

    public DeepSeekTranslationService(
        HttpClient httpClient,
        IOptions<TranslationValidationSettings> settings,
        ILogger<DeepSeekTranslationService> logger)
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
        if (string.IsNullOrWhiteSpace(_settings.DeepSeek.ApiKey))
        {
            _logger.LogDebug("DeepSeek API key not configured, skipping provider");
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
                    "DeepSeek back-translation attempt {Attempt}/{MaxRetries}: {TargetLang} → {SourceLang}",
                    attempt, MaxRetries, targetLanguage, sourceLanguage);

                var systemPrompt = "You are a professional translator. Provide ONLY the translated text, no explanations or notes.";
                var userPrompt = BuildBackTranslationPrompt(text, sourceLanguage, targetLanguage);

                var requestBody = new
                {
                    model = _settings.DeepSeek.Model,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.1,
                    max_tokens = 4096
                };

                var baseUrl = _settings.DeepSeek.BaseUrl.TrimEnd('/');
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        Encoding.UTF8,
                        "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {_settings.DeepSeek.ApiKey}");

                var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests && attempt < MaxRetries)
                {
                    _logger.LogWarning(
                        "DeepSeek rate limited on attempt {Attempt}, retrying in {Delay}s",
                        attempt, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                    continue;
                }

                if ((int)response.StatusCode >= 500 && attempt < MaxRetries)
                {
                    _logger.LogWarning(
                        "DeepSeek server error {StatusCode} on attempt {Attempt}, retrying in {Delay}s",
                        response.StatusCode, attempt, delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                    delay *= 2;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "DeepSeek API error: {StatusCode} - {Response}",
                        response.StatusCode, responseBody);
                    return BackTranslationResult.FailureResult(
                        $"DeepSeek API error: {response.StatusCode}", ProviderName);
                }

                var translatedText = ParseOpenAiCompatibleResponse(responseBody);

                if (string.IsNullOrWhiteSpace(translatedText))
                {
                    _logger.LogWarning("DeepSeek returned empty translation");
                    return BackTranslationResult.FailureResult(
                        "DeepSeek returned empty translation", ProviderName);
                }

                _logger.LogInformation(
                    "DeepSeek back-translation completed: {TargetLang} → {SourceLang}",
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
                    "DeepSeek HTTP error on attempt {Attempt}, retrying in {Delay}s",
                    attempt, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                delay *= 2;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "DeepSeek HTTP request failed after {MaxRetries} attempts", MaxRetries);
                return BackTranslationResult.FailureResult(
                    $"HTTP request failed: {ex.Message}", ProviderName);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse DeepSeek response");
                return BackTranslationResult.FailureResult(
                    $"Failed to parse response: {ex.Message}", ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeepSeek back-translation failed");
                return BackTranslationResult.FailureResult(
                    $"Back-translation failed: {ex.Message}", ProviderName);
            }
        }

        return BackTranslationResult.FailureResult(
            $"Failed after {MaxRetries} retry attempts", ProviderName);
    }

    /// <summary>
    /// Builds the back-translation user prompt.
    /// </summary>
    private static string BuildBackTranslationPrompt(string text, string sourceLanguage, string targetLanguage)
    {
        return $"""
            Translate the following text from {targetLanguage} back to {sourceLanguage}.
            Preserve the original meaning as accurately as possible. Maintain the same tone and register.
            Do not add or remove any content. Output ONLY the translation.

            Text to translate:
            {text}
            """;
    }

    /// <summary>
    /// Parses an OpenAI-compatible chat completion response.
    /// Response format: { "choices": [{ "message": { "content": "..." } }] }
    /// </summary>
    private static string ParseOpenAiCompatibleResponse(string responseBody)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);

        if (!jsonDoc.RootElement.TryGetProperty("choices", out var choices))
            return string.Empty;

        foreach (var choice in choices.EnumerateArray())
        {
            if (choice.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var contentEl))
            {
                return contentEl.GetString()?.Trim() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
