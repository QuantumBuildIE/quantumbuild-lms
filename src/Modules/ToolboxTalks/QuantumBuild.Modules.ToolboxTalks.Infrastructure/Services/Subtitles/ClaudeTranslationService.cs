using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;

/// <summary>
/// Translation service implementation using Claude (Anthropic) API.
/// Translates SRT subtitle content to different languages while preserving timing.
/// </summary>
public class ClaudeTranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<ClaudeTranslationService> _logger;

    public ClaudeTranslationService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<ClaudeTranslationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Translates SRT subtitle content to the target language using Claude API.
    /// </summary>
    public async Task<TranslationResult> TranslateSrtBatchAsync(
        string srtContent,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Translating SRT batch to {Language}", targetLanguage);

            var prompt = BuildTranslationPrompt(srtContent, targetLanguage);

            var requestBody = new
            {
                model = _settings.Claude.Model,
                max_tokens = _settings.Claude.MaxTokens,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.Claude.BaseUrl}/messages");
            request.Headers.Add("x-api-key", _settings.Claude.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Claude API error: {StatusCode} - {Response}", response.StatusCode, responseBody);
                return TranslationResult.FailureResult($"Claude API error: {response.StatusCode}");
            }

            var translatedText = ParseTranslationResponse(responseBody);

            if (string.IsNullOrWhiteSpace(translatedText))
            {
                _logger.LogWarning("Translation returned empty content for {Language}", targetLanguage);
                return TranslationResult.FailureResult($"Translation to {targetLanguage} returned empty content");
            }

            _logger.LogInformation("Translation to {Language} completed", targetLanguage);

            return TranslationResult.SuccessResult(translatedText);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during translation to {Language}", targetLanguage);
            return TranslationResult.FailureResult($"HTTP request failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse translation response for {Language}", targetLanguage);
            return TranslationResult.FailureResult($"Failed to parse translation response: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation to {Language} failed", targetLanguage);
            return TranslationResult.FailureResult($"Translation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the translation prompt for Claude.
    /// Instructs the model to preserve SRT formatting while translating only the text.
    /// </summary>
    private static string BuildTranslationPrompt(string srtContent, string targetLanguage)
    {
        return $@"Translate the following SRT subtitle text to {targetLanguage}.
Keep the exact same format with numbers and timestamps, only translate the text.
Return only the translated SRT, nothing else:

{srtContent}";
    }

    /// <summary>
    /// Parses the Claude API response to extract the translated text.
    /// </summary>
    private static string ParseTranslationResponse(string responseBody)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);

        if (!jsonDoc.RootElement.TryGetProperty("content", out var contentArray))
            return string.Empty;

        foreach (var item in contentArray.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var textEl))
            {
                return textEl.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }
}
