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
/// Detects the regional dialect/variant of a text sample using Claude Haiku
/// via the Anthropic Messages API.
/// </summary>
public class DialectDetectionService : IDialectDetectionService
{
    private const string HaikuModel = "claude-haiku-4-5-20251001";

    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly IAiUsageLogger _aiUsageLogger;
    private readonly ILogger<DialectDetectionService> _logger;

    public DialectDetectionService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        IAiUsageLogger aiUsageLogger,
        ILogger<DialectDetectionService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _aiUsageLogger = aiUsageLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DialectDetectionResult> DetectAsync(
        string text,
        string expectedLanguageCode,
        Guid tenantId,
        Guid? userId = null,
        Guid? toolboxTalkId = null,
        bool isSystemCall = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return DialectDetectionResult.FailureResult("Text sample is empty");
        }

        try
        {
            _logger.LogInformation(
                "DialectDetectionService.DetectAsync called. ExpectedLanguage: {Lang}, TextLength: {Len}",
                expectedLanguageCode, text.Length);

            var prompt = BuildPrompt(text, expectedLanguageCode);
            var responseText = await CallClaudeApiAsync(prompt, tenantId, userId, toolboxTalkId, isSystemCall, cancellationToken);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return DialectDetectionResult.FailureResult("Claude API returned empty response");
            }

            return ParseResponse(responseText);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP request failed during dialect detection. StatusCode: {StatusCode}",
                ex.StatusCode);
            return DialectDetectionResult.FailureResult($"HTTP request failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse dialect detection response");
            return DialectDetectionResult.FailureResult($"Failed to parse response: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Dialect detection failed with {ExceptionType}: {Message}",
                ex.GetType().Name, ex.Message);
            return DialectDetectionResult.FailureResult($"Detection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the dialect detection prompt.
    /// </summary>
    private static string BuildPrompt(string text, string expectedLanguageCode) =>
        $"""
        Analyse the following text sample and detect its regional dialect/variant.
        The expected language code is "{expectedLanguageCode}".

        Text sample:
        ---
        {text}
        ---

        Respond with ONLY a JSON object (no markdown, no explanation outside JSON) with these fields:
        - "languageCode": ISO 639-1 code (e.g. "pt", "en", "es")
        - "variant": regional description (e.g. "Brazilian Portuguese", "European Portuguese", "British English", "Latin American Spanish")
        - "confidence": one of "High", "Medium", or "Low"
        - "reasoning": 1-2 sentences explaining the dialect indicators found
        - "backTranslationGuidance": specific instructions for a translator to produce text in this exact dialect/variant (vocabulary preferences, spelling conventions, grammar patterns to use)
        """;

    /// <summary>
    /// Calls the Claude API (Haiku model) with the given prompt.
    /// Uses AnthropicResponseParser for response extraction and IAiUsageLogger for token tracking.
    /// </summary>
    private async Task<string> CallClaudeApiAsync(
        string prompt,
        Guid tenantId,
        Guid? userId,
        Guid? toolboxTalkId,
        bool isSystemCall,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.Claude.ApiKey))
        {
            throw new InvalidOperationException(
                "Claude API key is not configured. Please set the SubtitleProcessing__Claude__ApiKey environment variable.");
        }

        var requestBody = new
        {
            model = HaikuModel,
            max_tokens = 1024,
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

        _logger.LogInformation(
            "Calling Claude API (Haiku) at {BaseUrl} for dialect detection",
            _settings.Claude.BaseUrl);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Claude API error: {StatusCode} - {Response}",
                response.StatusCode, responseBody);
            throw new HttpRequestException($"Claude API error: {response.StatusCode} - {responseBody}");
        }

        var parsed = AnthropicResponseParser.Parse(responseBody);

        await _aiUsageLogger.LogAsync(
            tenantId,
            AiOperationCategory.DialectDetection,
            parsed.Model,
            parsed.InputTokens,
            parsed.OutputTokens,
            isSystemCall,
            userId,
            toolboxTalkId,
            cancellationToken);

        return parsed.ContentText;
    }

    /// <summary>
    /// Parses the structured JSON response from Claude into a DialectDetectionResult.
    /// </summary>
    private DialectDetectionResult ParseResponse(string responseText)
    {
        // Strip markdown code fences if present
        var json = responseText.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            if (firstNewline >= 0)
                json = json[(firstNewline + 1)..];
            if (json.EndsWith("```"))
                json = json[..^3];
            json = json.Trim();
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var languageCode = root.GetProperty("languageCode").GetString() ?? string.Empty;
        var variant = root.GetProperty("variant").GetString() ?? string.Empty;
        var confidenceStr = root.GetProperty("confidence").GetString() ?? "Low";
        var reasoning = root.GetProperty("reasoning").GetString() ?? string.Empty;
        var guidance = root.GetProperty("backTranslationGuidance").GetString() ?? string.Empty;

        var confidence = confidenceStr switch
        {
            "High" => DialectConfidence.High,
            "Medium" => DialectConfidence.Medium,
            _ => DialectConfidence.Low
        };

        _logger.LogInformation(
            "Dialect detected: {Variant} ({Code}), Confidence: {Confidence}",
            variant, languageCode, confidence);

        return DialectDetectionResult.SuccessResult(languageCode, variant, confidence, reasoning, guidance);
    }
}
