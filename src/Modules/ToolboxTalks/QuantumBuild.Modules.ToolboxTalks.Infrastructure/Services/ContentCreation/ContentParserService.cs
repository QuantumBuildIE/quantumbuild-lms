using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Application.Prompts;
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
    private readonly string _claudeModel;
    private readonly IAiUsageLogger _aiUsageLogger;
    private readonly ILogger<ContentParserService> _logger;

    private const int CourseThreshold = 3;

    public ContentParserService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        IOptions<AIProviderOptions> aiProviders,
        IAiUsageLogger aiUsageLogger,
        ILogger<ContentParserService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _claudeModel = aiProviders.Value.Anthropic.Models.Sonnet;
        _aiUsageLogger = aiUsageLogger;
        _logger = logger;
    }

    public async Task<ContentParseResult> ParseContentAsync(
        string rawText,
        InputMode inputModeHint,
        Guid tenantId,
        Guid? userId = null,
        bool preserveSourceWording = false,
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

            var sourceDescription = inputModeHint switch
            {
                InputMode.Pdf => "extracted from a PDF document",
                InputMode.Video => "transcribed from a video",
                InputMode.Text => "provided as plain text",
                _ => "provided as raw content"
            };

            var prompt = SectionGenerationPrompts.BuildSectionPrompt(
                content: rawText,
                sourceDescription: sourceDescription,
                minimumSections: 2,
                hasVideo: inputModeHint == InputMode.Video,
                hasPdf: inputModeHint == InputMode.Pdf,
                preserveSourceWording: preserveSourceWording);

            var requestBody = new
            {
                model = _claudeModel,
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

            _logger.LogInformation("[ContentParserService] Parsing content ({InputMode}, verbatim={Verbatim}) with Claude AI",
                inputModeHint, preserveSourceWording);

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

            var parsed = AnthropicResponseParser.Parse(responseBody);
            var sections = ParseSectionsFromContentText(parsed.ContentText);
            var tokensUsed = parsed.InputTokens + parsed.OutputTokens;
            var suggestedType = SuggestOutputType(sections.Count);

            await _aiUsageLogger.LogAsync(
                tenantId,
                AiOperationCategory.ContentParsing,
                parsed.Model,
                parsed.InputTokens,
                parsed.OutputTokens,
                isSystemCall: false,
                userId: userId,
                referenceEntityId: null,
                cancellationToken);

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
        // Always default to Lesson — user can manually switch to Course if desired
        return OutputType.Lesson;
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

    private static List<ParsedSection> ParseSectionsFromContentText(string textContent)
    {
        var sections = new List<ParsedSection>();

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
                // BuildSectionPrompt emits "sortOrder"; fall back to "suggestedOrder" for
                // any cached responses produced by the legacy BuildParsePrompt shape.
                var suggestedOrder =
                    (element.TryGetProperty("sortOrder", out var so) ? (int?)so.GetInt32() : null)
                    ?? (element.TryGetProperty("suggestedOrder", out var sgo) ? (int?)sgo.GetInt32() : null)
                    ?? (sections.Count + 1);

                sections.Add(new ParsedSection(title, sectionContent, suggestedOrder));
            }
        }

        return sections;
    }
}
