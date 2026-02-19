using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Prompts;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Slideshow;

/// <summary>
/// AI-powered slideshow generation service using Claude (Anthropic) API.
/// Sends a PDF document to Claude and receives a complete, self-contained HTML slideshow.
/// </summary>
public class AiSlideshowGenerationService : IAiSlideshowGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<AiSlideshowGenerationService> _logger;

    public AiSlideshowGenerationService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<AiSlideshowGenerationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateSlideshowFromPdfAsync(
        byte[] pdfBytes,
        string documentTitle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.Claude.ApiKey))
            {
                _logger.LogError("Claude API key is not configured");
                return Result.Fail<string>("Claude API key not configured");
            }

            _logger.LogInformation(
                "Generating AI slideshow for document: {Title}, PDF size: {Size} bytes",
                documentTitle, pdfBytes.Length);

            var pdfBase64 = Convert.ToBase64String(pdfBytes);
            var prompt = SlideshowGenerationPrompts.GetPdfSlideshowPrompt();

            var requestBody = new
            {
                model = _settings.Claude.Model,
                max_tokens = 16000,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "document",
                                source = new
                                {
                                    type = "base64",
                                    media_type = "application/pdf",
                                    data = pdfBase64
                                }
                            },
                            new
                            {
                                type = "text",
                                source = (object?)null,
                                text = prompt
                            }
                        }
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.Claude.BaseUrl}/messages");
            request.Headers.Add("x-api-key", _settings.Claude.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                }),
                Encoding.UTF8,
                "application/json");

            _logger.LogInformation(
                "Sending PDF to Claude for slideshow generation (document: {Title})",
                documentTitle);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Claude API error for slideshow generation: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                return Result.Fail<string>($"Claude API error: {response.StatusCode}");
            }

            var html = ExtractHtmlFromResponse(responseBody);

            if (string.IsNullOrWhiteSpace(html))
            {
                _logger.LogWarning("Claude returned empty response for slideshow generation");
                return Result.Fail<string>("AI returned empty response");
            }

            // Validate it looks like HTML
            if (!html.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase) &&
                !html.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Claude response doesn't appear to be valid HTML: {Preview}",
                    html[..Math.Min(200, html.Length)]);
                return Result.Fail<string>("AI response is not valid HTML");
            }

            // Log token usage
            LogTokenUsage(responseBody);

            _logger.LogInformation(
                "Successfully generated HTML slideshow for {Title}, size: {Size} characters",
                documentTitle, html.Length);

            return Result.Ok(html);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during slideshow generation for {Title}", documentTitle);
            return Result.Fail<string>($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI slideshow for document: {Title}", documentTitle);
            return Result.Fail<string>($"Failed to generate slideshow: {ex.Message}");
        }
    }

    public async Task<Result<string>> GenerateSlideshowFromTranscriptAsync(
        string transcriptText,
        string documentTitle,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.Claude.ApiKey))
            {
                _logger.LogError("Claude API key is not configured");
                return Result.Fail<string>("Claude API key not configured");
            }

            _logger.LogInformation(
                "Generating AI slideshow from transcript for document: {Title}, transcript length: {Length} chars",
                documentTitle, transcriptText.Length);

            var prompt = SlideshowGenerationPrompts.GetTranscriptSlideshowPrompt();

            var requestBody = new
            {
                model = _settings.Claude.Model,
                max_tokens = 16000,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = $"Document title: {documentTitle}\n\nVideo Transcript:\n\n{transcriptText}"
                            },
                            new
                            {
                                type = "text",
                                text = prompt
                            }
                        }
                    }
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
                "Sending transcript to Claude for slideshow generation (document: {Title})",
                documentTitle);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Claude API error for transcript slideshow generation: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                return Result.Fail<string>($"Claude API error: {response.StatusCode}");
            }

            var html = ExtractHtmlFromResponse(responseBody);

            if (string.IsNullOrWhiteSpace(html))
            {
                _logger.LogWarning("Claude returned empty response for transcript slideshow generation");
                return Result.Fail<string>("AI returned empty response");
            }

            if (!html.TrimStart().StartsWith("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase) &&
                !html.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Claude response doesn't appear to be valid HTML: {Preview}",
                    html[..Math.Min(200, html.Length)]);
                return Result.Fail<string>("AI response is not valid HTML");
            }

            LogTokenUsage(responseBody);

            _logger.LogInformation(
                "Successfully generated HTML slideshow from transcript for {Title}, size: {Size} characters",
                documentTitle, html.Length);

            return Result.Ok(html);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during transcript slideshow generation for {Title}", documentTitle);
            return Result.Fail<string>($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI slideshow from transcript for document: {Title}", documentTitle);
            return Result.Fail<string>($"Failed to generate slideshow: {ex.Message}");
        }
    }

    private string? ExtractHtmlFromResponse(string responseBody)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);

        if (!jsonDoc.RootElement.TryGetProperty("content", out var contentArray))
        {
            _logger.LogWarning("No content property found in Claude response");
            return null;
        }

        foreach (var item in contentArray.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var textEl))
            {
                return textEl.GetString();
            }
        }

        return null;
    }

    private void LogTokenUsage(string responseBody)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(responseBody);
            if (jsonDoc.RootElement.TryGetProperty("usage", out var usageEl))
            {
                var inputTokens = usageEl.TryGetProperty("input_tokens", out var inputEl) ? inputEl.GetInt32() : 0;
                var outputTokens = usageEl.TryGetProperty("output_tokens", out var outputEl) ? outputEl.GetInt32() : 0;
                _logger.LogInformation(
                    "Slideshow generation token usage: input={InputTokens}, output={OutputTokens}, total={TotalTokens}",
                    inputTokens, outputTokens, inputTokens + outputTokens);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse token usage from response");
        }
    }

}
