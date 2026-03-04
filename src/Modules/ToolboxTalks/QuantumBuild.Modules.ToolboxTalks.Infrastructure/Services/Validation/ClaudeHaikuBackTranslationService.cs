using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Back-translation service using Claude Haiku via the Anthropic Messages API.
/// Provider A in the consensus engine — always available when the Claude API key is configured.
/// </summary>
public class ClaudeHaikuBackTranslationService : IClaudeHaikuBackTranslationService
{
    private const string ProviderName = "Claude Haiku";
    private const string HaikuModel = "claude-haiku-4-5-20251001";

    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<ClaudeHaikuBackTranslationService> _logger;

    public ClaudeHaikuBackTranslationService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<ClaudeHaikuBackTranslationService> logger)
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
        if (string.IsNullOrWhiteSpace(_settings.Claude.ApiKey))
        {
            _logger.LogDebug("Claude API key not configured — skipping Haiku back-translation");
            return null;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return BackTranslationResult.FailureResult("Input text is empty", ProviderName);
        }

        try
        {
            _logger.LogInformation(
                "Claude Haiku back-translating {TextLength} chars from {Target} → {Source}",
                text.Length, targetLanguage, sourceLanguage);

            var prompt = $"""
                You are a professional translator. Translate the following text from {targetLanguage} back into {sourceLanguage}.
                Return ONLY the translated text with no explanations, no preamble, no markdown.

                Text to translate:
                {text}
                """;

            var requestBody = new
            {
                model = HaikuModel,
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
                    "Claude Haiku back-translation failed: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                return BackTranslationResult.FailureResult(
                    $"API error: {response.StatusCode}", ProviderName);
            }

            var backTranslated = ParseClaudeResponse(responseBody);

            if (string.IsNullOrWhiteSpace(backTranslated))
            {
                return BackTranslationResult.FailureResult(
                    "Empty response from Claude Haiku", ProviderName);
            }

            _logger.LogInformation(
                "Claude Haiku back-translation complete: {ResultLength} chars",
                backTranslated.Length);

            return BackTranslationResult.SuccessResult(backTranslated, ProviderName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude Haiku back-translation failed with {ExType}", ex.GetType().Name);
            return BackTranslationResult.FailureResult($"Exception: {ex.Message}", ProviderName);
        }
    }

    /// <summary>
    /// Extracts the text content from a Claude Messages API response.
    /// </summary>
    private static string ParseClaudeResponse(string responseBody)
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
