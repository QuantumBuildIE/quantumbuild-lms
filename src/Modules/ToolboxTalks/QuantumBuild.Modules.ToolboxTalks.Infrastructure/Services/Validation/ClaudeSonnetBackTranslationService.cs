using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Back-translation service using Claude Sonnet (claude-sonnet-4-20250514) via the Anthropic Messages API.
/// Provider D in the consensus engine — Round 3 final tiebreaker.
/// Replaced DeepSeek in pipeline v6.4 for GDPR compliance.
/// </summary>
public class ClaudeSonnetBackTranslationService : IClaudeSonnetBackTranslationService
{
    private const string ProviderName = "Claude Sonnet";
    private const string SonnetModel = "claude-sonnet-4-20250514";

    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly IAiUsageLogger _aiUsageLogger;
    private readonly ILogger<ClaudeSonnetBackTranslationService> _logger;

    public ClaudeSonnetBackTranslationService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        IAiUsageLogger aiUsageLogger,
        ILogger<ClaudeSonnetBackTranslationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _aiUsageLogger = aiUsageLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BackTranslationResult?> BackTranslateAsync(
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default,
        Guid tenantId = default,
        Guid? userId = null,
        bool isSystemCall = true,
        Guid? toolboxTalkId = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.Claude.ApiKey))
        {
            _logger.LogDebug("Claude API key not configured — skipping Sonnet back-translation");
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return BackTranslationResult.FailureResult("Input text is empty", ProviderName);
        }

        try
        {
            _logger.LogInformation(
                "Claude Sonnet back-translating {TextLength} chars from {Target} → {Source}",
                text.Length, targetLanguage, sourceLanguage);

            var prompt = $"""
                You are a professional translator. Translate the following text from {targetLanguage} back into {sourceLanguage}.
                Return ONLY the translated text with no explanations, no preamble, no markdown.

                Text to translate:
                {text}
                """;

            var requestBody = new
            {
                model = SonnetModel,
                max_tokens = 4096,
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
                _logger.LogError(
                    "Claude Sonnet back-translation failed: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                return BackTranslationResult.FailureResult(
                    $"API error: {response.StatusCode}", ProviderName);
            }

            var parsed = AnthropicResponseParser.Parse(responseBody);

            await _aiUsageLogger.LogAsync(
                tenantId,
                AiOperationCategory.BackTranslation,
                parsed.Model,
                parsed.InputTokens,
                parsed.OutputTokens,
                isSystemCall: isSystemCall,
                userId: userId,
                referenceEntityId: toolboxTalkId,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(parsed.ContentText))
            {
                return BackTranslationResult.FailureResult(
                    "Empty response from Claude Sonnet", ProviderName);
            }

            _logger.LogInformation(
                "Claude Sonnet back-translation complete: {ResultLength} chars",
                parsed.ContentText.Length);

            return BackTranslationResult.SuccessResult(parsed.ContentText, ProviderName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude Sonnet back-translation failed with {ExType}", ex.GetType().Name);
            return BackTranslationResult.FailureResult($"Exception: {ex.Message}", ProviderName);
        }
    }
}
