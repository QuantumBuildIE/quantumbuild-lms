using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;
using QuantumBuild.Modules.ToolboxTalks.Application.Prompts;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Translations;

/// <summary>
/// Content translation service implementation using Claude (Anthropic) API.
/// Translates text and HTML content to different languages.
/// </summary>
public class ContentTranslationService : IContentTranslationService
{
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly IAiUsageLogger _aiUsageLogger;
    private readonly ILogger<ContentTranslationService> _logger;

    public ContentTranslationService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        IAiUsageLogger aiUsageLogger,
        ILogger<ContentTranslationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _aiUsageLogger = aiUsageLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ContentTranslationResult> TranslateTextAsync(
        string text,
        string targetLanguage,
        bool isHtml = false,
        CancellationToken cancellationToken = default,
        string? sourceLanguage = null,
        string? sectorKey = null,
        bool isSafetyCritical = false,
        IEnumerable<GlossaryTermInstruction>? glossaryTerms = null,
        Guid tenantId = default,
        Guid? userId = null,
        bool isSystemCall = false,
        Guid? toolboxTalkId = null)
    {
        var source = sourceLanguage ?? "English";

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation(
                "[DEBUG] ContentTranslationService.TranslateTextAsync called with empty/null text for {Language}. Returning empty success.",
                targetLanguage);
            return ContentTranslationResult.SuccessResult(string.Empty);
        }

        try
        {
            var useTiered = sectorKey != null || isSafetyCritical || glossaryTerms != null;

            _logger.LogInformation(
                "[DEBUG] ContentTranslationService.TranslateTextAsync called. " +
                "SourceLanguage: {SourceLanguage}, TargetLanguage: {Language}, IsHtml: {IsHtml}, TextLength: {TextLength}, " +
                "Tiered: {Tiered}, SectorKey: {SectorKey}, SafetyCritical: {SafetyCritical}, TextPreview: {Preview}",
                source, targetLanguage, isHtml, text.Length,
                useTiered, sectorKey ?? "(none)", isSafetyCritical,
                text.Length > 80 ? text.Substring(0, 80) + "..." : text);

            var prompt = useTiered
                ? TranslationPrompts.BuildTranslationPrompt(text, source, targetLanguage, isHtml, sectorKey, isSafetyCritical, glossaryTerms)
                : TranslationPrompts.BuildGenericTranslationPrompt(text, source, targetLanguage, isHtml);
            var parsed = await CallClaudeApiAsync(prompt, cancellationToken);

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
                _logger.LogWarning(
                    "[DEBUG] Claude API returned empty/whitespace translation for {Language}. Input length was {InputLength}",
                    targetLanguage, text.Length);
                return ContentTranslationResult.FailureResult($"Translation to {targetLanguage} returned empty content");
            }

            _logger.LogInformation(
                "[DEBUG] Translation to {Language} completed successfully. " +
                "InputLength: {InputLength}, OutputLength: {OutputLength}",
                targetLanguage, text.Length, parsed.ContentText.Length);
            return ContentTranslationResult.SuccessResult(parsed.ContentText);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "[DEBUG] HTTP request failed during translation to {Language}. StatusCode: {StatusCode}",
                targetLanguage, ex.StatusCode);
            return ContentTranslationResult.FailureResult($"HTTP request failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[DEBUG] Failed to parse translation response for {Language}", targetLanguage);
            return ContentTranslationResult.FailureResult($"Failed to parse translation response: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DEBUG] Translation to {Language} failed with {ExceptionType}: {Message}",
                targetLanguage, ex.GetType().Name, ex.Message);
            return ContentTranslationResult.FailureResult($"Translation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ContentTranslationResult> SendCustomPromptAsync(
        string prompt,
        CancellationToken cancellationToken = default,
        Guid tenantId = default,
        Guid? userId = null,
        bool isSystemCall = false,
        Guid? toolboxTalkId = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return ContentTranslationResult.FailureResult("Prompt cannot be empty");
        }

        try
        {
            _logger.LogInformation(
                "[DEBUG] ContentTranslationService.SendCustomPromptAsync called. PromptLength: {Length}",
                prompt.Length);

            var parsed = await CallClaudeApiAsync(prompt, cancellationToken);

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
                _logger.LogWarning(
                    "[DEBUG] Claude API returned empty response for custom prompt. PromptLength: {Length}",
                    prompt.Length);
                return ContentTranslationResult.FailureResult("AI returned empty response");
            }

            _logger.LogInformation(
                "[DEBUG] Custom prompt completed. ResponseLength: {Length}",
                parsed.ContentText.Length);
            return ContentTranslationResult.SuccessResult(parsed.ContentText);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "[DEBUG] HTTP request failed for custom prompt. StatusCode: {StatusCode}",
                ex.StatusCode);
            return ContentTranslationResult.FailureResult($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DEBUG] Custom prompt failed with {ExceptionType}: {Message}",
                ex.GetType().Name, ex.Message);
            return ContentTranslationResult.FailureResult($"Custom prompt failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<BatchTranslationResult> TranslateBatchAsync(
        IEnumerable<TranslationItem> items,
        string targetLanguage,
        CancellationToken cancellationToken = default,
        string? sourceLanguage = null,
        string? sectorKey = null,
        bool isSafetyCritical = false,
        IEnumerable<GlossaryTermInstruction>? glossaryTerms = null,
        Guid tenantId = default,
        Guid? userId = null,
        bool isSystemCall = false,
        Guid? toolboxTalkId = null)
    {
        var source = sourceLanguage ?? "English";
        var itemsList = items.ToList();
        if (itemsList.Count == 0)
        {
            return BatchTranslationResult.SuccessResult(new Dictionary<string, ContentTranslationResult>());
        }

        try
        {
            _logger.LogInformation("Translating batch of {Count} items from {Source} to {Language}", itemsList.Count, source, targetLanguage);

            var prompt = TranslationPrompts.BuildBatchTranslationPrompt(itemsList, source, targetLanguage, sectorKey, isSafetyCritical, glossaryTerms);
            var parsed = await CallClaudeApiAsync(prompt, cancellationToken);

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

            var results = ParseBatchResponse(parsed.ContentText, itemsList);

            _logger.LogInformation("Batch translation to {Language} completed", targetLanguage);
            return BatchTranslationResult.SuccessResult(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch translation to {Language} failed", targetLanguage);
            return BatchTranslationResult.FailureResult($"Batch translation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Calls the Claude API with the given prompt.
    /// </summary>
    private async Task<AnthropicParsedResponse> CallClaudeApiAsync(string prompt, CancellationToken cancellationToken)
    {
        // Validate API key is configured
        if (string.IsNullOrWhiteSpace(_settings.Claude.ApiKey))
        {
            _logger.LogError("Claude API key is not configured. Set SubtitleProcessing__Claude__ApiKey environment variable.");
            throw new InvalidOperationException("Claude API key is not configured. Please set the SubtitleProcessing__Claude__ApiKey environment variable.");
        }

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

        _logger.LogInformation(
            "[DEBUG] Calling Claude API at {BaseUrl} with model {Model}",
            _settings.Claude.BaseUrl, _settings.Claude.Model);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation(
            "[DEBUG] Claude API response received. StatusCode: {StatusCode}, ResponseBodyLength: {Length}",
            response.StatusCode, responseBody?.Length ?? 0);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "[DEBUG] Claude API error: {StatusCode} - {Response}",
                response.StatusCode, responseBody);
            throw new HttpRequestException($"Claude API error: {response.StatusCode} - {responseBody}");
        }

        var parsed = AnthropicResponseParser.Parse(responseBody);
        _logger.LogInformation(
            "[DEBUG] Parsed Claude response. IsEmpty: {IsEmpty}, ResultLength: {Length}",
            string.IsNullOrWhiteSpace(parsed.ContentText), parsed.ContentText?.Length ?? 0);

        return parsed;
    }

    /// <summary>
    /// Parses a batch translation response into individual results.
    /// </summary>
    private Dictionary<string, ContentTranslationResult> ParseBatchResponse(
        string responseText,
        List<TranslationItem> items)
    {
        var results = new Dictionary<string, ContentTranslationResult>();

        try
        {
            // Try to extract JSON array from the response
            var jsonStart = responseText.IndexOf('[');
            var jsonEnd = responseText.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var translations = JsonSerializer.Deserialize<List<string>>(jsonText);

                if (translations != null)
                {
                    for (int i = 0; i < items.Count && i < translations.Count; i++)
                    {
                        results[items[i].Key] = ContentTranslationResult.SuccessResult(translations[i]);
                    }

                    // Handle any items that didn't get a translation
                    for (int i = translations.Count; i < items.Count; i++)
                    {
                        results[items[i].Key] = ContentTranslationResult.FailureResult("No translation returned");
                    }

                    return results;
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse batch response as JSON array");
        }

        // Fallback: return failures - don't mask translation errors with original text
        foreach (var item in items)
        {
            results[item.Key] = ContentTranslationResult.FailureResult("Failed to parse translation response");
        }

        return results;
    }
}
