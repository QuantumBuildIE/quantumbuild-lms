using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.ContentCreation;

/// <summary>
/// Uses Claude Sonnet to parse raw text into logical sections.
/// </summary>
public class ContentParserService : IContentParserService
{
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<ContentParserService> _logger;

    private const int CourseThreshold = 3;

    public ContentParserService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<ContentParserService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ContentParseResult> ParseContentAsync(
        string rawText,
        InputMode inputModeHint,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.Claude.ApiKey))
            {
                _logger.LogError("Claude API key is not configured");
                return new ContentParseResult(
                    Success: false,
                    Sections: new List<ParsedSection>(),
                    SuggestedOutputType: OutputType.Lesson,
                    ErrorMessage: "Claude API key not configured");
            }

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new ContentParseResult(
                    Success: false,
                    Sections: new List<ParsedSection>(),
                    SuggestedOutputType: OutputType.Lesson,
                    ErrorMessage: "No content provided for parsing");
            }

            var sourceHint = inputModeHint switch
            {
                InputMode.Pdf => "extracted from a PDF document",
                InputMode.Video => "transcribed from a video",
                InputMode.Text => "provided as plain text",
                _ => "provided as raw content"
            };

            var prompt = BuildParsePrompt(rawText, sourceHint);

            var requestBody = new
            {
                model = _settings.Claude.Model,
                max_tokens = 8000,
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

            _logger.LogInformation("[ContentParserService] Parsing content ({InputMode}) with Claude AI", inputModeHint);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[ContentParserService] Claude API error: {StatusCode} - {Response}",
                    response.StatusCode, responseBody);
                return new ContentParseResult(
                    Success: false,
                    Sections: new List<ParsedSection>(),
                    SuggestedOutputType: OutputType.Lesson,
                    ErrorMessage: $"Claude API error: {response.StatusCode}");
            }

            var (sections, tokensUsed) = ParseSectionsFromResponse(responseBody);
            var suggestedType = SuggestOutputType(sections.Count);

            _logger.LogInformation(
                "[ContentParserService] Parsed {Count} sections ({TokensUsed} tokens), suggested output: {OutputType}",
                sections.Count, tokensUsed, suggestedType);

            return new ContentParseResult(
                Success: true,
                Sections: sections,
                SuggestedOutputType: suggestedType,
                TokensUsed: tokensUsed);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[ContentParserService] HTTP request failed during parsing");
            return new ContentParseResult(
                Success: false,
                Sections: new List<ParsedSection>(),
                SuggestedOutputType: OutputType.Lesson,
                ErrorMessage: $"HTTP request failed: {ex.Message}");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[ContentParserService] Failed to parse AI response");
            return new ContentParseResult(
                Success: false,
                Sections: new List<ParsedSection>(),
                SuggestedOutputType: OutputType.Lesson,
                ErrorMessage: $"Failed to parse response: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ContentParserService] Unexpected error during parsing");
            return new ContentParseResult(
                Success: false,
                Sections: new List<ParsedSection>(),
                SuggestedOutputType: OutputType.Lesson,
                ErrorMessage: $"Unexpected error: {ex.Message}");
        }
    }

    public OutputType SuggestOutputType(int sectionCount)
    {
        return sectionCount >= CourseThreshold ? OutputType.Course : OutputType.Lesson;
    }

    private static string BuildParsePrompt(string rawText, string sourceHint)
    {
        return $$"""
            You are an expert at structuring workplace safety training content.

            The following text was {{sourceHint}}. Parse it into logical training sections.

            For each section, provide:
            - A clear, descriptive title
            - The content (cleaned up, formatted as HTML with <p>, <ul>, <li>, <strong> tags as appropriate)
            - A suggested order number (starting from 1)

            Guidelines:
            - Each section should cover a single coherent topic
            - Remove any filler, repetition, or off-topic content
            - Clean up transcription artefacts if present
            - Ensure content is suitable for workplace training
            - Use clear, professional language
            - Minimum 2 sections, aim for 3-7 sections depending on content length

            Return your response as a JSON array only, with no surrounding text:
            [
              {
                "title": "Section Title",
                "content": "<p>HTML content here</p>",
                "suggestedOrder": 1
              }
            ]

            TEXT TO PARSE:
            {{rawText}}
            """;
    }

    private (List<ParsedSection> Sections, int TokensUsed) ParseSectionsFromResponse(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var tokensUsed = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("input_tokens", out var input))
                tokensUsed += input.GetInt32();
            if (usage.TryGetProperty("output_tokens", out var output))
                tokensUsed += output.GetInt32();
        }

        var sections = new List<ParsedSection>();

        if (root.TryGetProperty("content", out var content) && content.GetArrayLength() > 0)
        {
            var textContent = content[0].GetProperty("text").GetString() ?? "";

            // Extract JSON array from response (may have surrounding text)
            var jsonStart = textContent.IndexOf('[');
            var jsonEnd = textContent.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonArray = textContent[jsonStart..(jsonEnd + 1)];
                using var sectionsDoc = JsonDocument.Parse(jsonArray);

                foreach (var element in sectionsDoc.RootElement.EnumerateArray())
                {
                    var title = element.GetProperty("title").GetString() ?? "Untitled";
                    var sectionContent = element.GetProperty("content").GetString() ?? "";
                    var suggestedOrder = element.TryGetProperty("suggestedOrder", out var order)
                        ? order.GetInt32()
                        : sections.Count + 1;

                    sections.Add(new ParsedSection(title, sectionContent, suggestedOrder));
                }
            }
        }

        return (sections, tokensUsed);
    }
}
