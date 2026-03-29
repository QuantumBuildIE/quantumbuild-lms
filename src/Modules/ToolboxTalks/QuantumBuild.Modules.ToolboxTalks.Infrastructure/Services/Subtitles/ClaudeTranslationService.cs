using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Prompts;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
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
    private readonly IAiUsageLogger _aiUsageLogger;
    private readonly ILogger<ClaudeTranslationService> _logger;

    public ClaudeTranslationService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        IAiUsageLogger aiUsageLogger,
        ILogger<ClaudeTranslationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _aiUsageLogger = aiUsageLogger;
        _logger = logger;
    }

    /// <summary>
    /// Translates SRT subtitle content to the target language using Claude API.
    /// </summary>
    public async Task<TranslationResult> TranslateSrtBatchAsync(
        string srtContent,
        string targetLanguage,
        CancellationToken cancellationToken = default,
        Guid tenantId = default,
        Guid? userId = null,
        bool isSystemCall = false,
        Guid? toolboxTalkId = null)
    {
        try
        {
            _logger.LogInformation("Translating SRT batch to {Language}", targetLanguage);

            var prompt = TranslationPrompts.BuildSrtTranslationPrompt(srtContent, targetLanguage);

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

            var parsed = AnthropicResponseParser.Parse(responseBody);

            await _aiUsageLogger.LogAsync(
                tenantId,
                AiOperationCategory.ContentTranslation,
                parsed.Model,
                parsed.InputTokens,
                parsed.OutputTokens,
                isSystemCall: isSystemCall,
                userId: userId,
                referenceEntityId: toolboxTalkId,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(parsed.ContentText))
            {
                _logger.LogWarning("Translation returned empty content for {Language}", targetLanguage);
                return TranslationResult.FailureResult($"Translation to {targetLanguage} returned empty content");
            }

            _logger.LogInformation("Translation to {Language} completed", targetLanguage);

            return TranslationResult.SuccessResult(parsed.ContentText);
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
}
