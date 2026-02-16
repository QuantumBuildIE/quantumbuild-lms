using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// AI-powered section generation service using Claude (Anthropic) API.
/// Generates concise sections from video transcripts and/or PDF content.
/// </summary>
public class AiSectionGenerationService : IAiSectionGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<AiSectionGenerationService> _logger;

    public AiSectionGenerationService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<AiSectionGenerationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<SectionGenerationResult> GenerateSectionsAsync(
        Guid toolboxTalkId,
        string combinedContent,
        bool hasVideoContent,
        bool hasPdfContent,
        int minimumSections = 7,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.Claude.ApiKey))
            {
                _logger.LogError("Claude API key is not configured");
                return new SectionGenerationResult(
                    Success: false,
                    Sections: new List<GeneratedSection>(),
                    ErrorMessage: "Claude API key not configured",
                    TokensUsed: 0);
            }

            if (string.IsNullOrWhiteSpace(combinedContent))
            {
                _logger.LogWarning("No content provided for section generation for toolbox talk {Id}", toolboxTalkId);
                return new SectionGenerationResult(
                    Success: false,
                    Sections: new List<GeneratedSection>(),
                    ErrorMessage: "No content provided for section generation",
                    TokensUsed: 0);
            }

            var sourceDescription = (hasVideoContent, hasPdfContent) switch
            {
                (true, true) => "video transcript and PDF document",
                (true, false) => "video transcript",
                (false, true) => "PDF document",
                _ => "provided content"
            };

            var prompt = BuildSectionPrompt(combinedContent, sourceDescription, minimumSections, hasVideoContent, hasPdfContent);

            var requestBody = new
            {
                model = _settings.Claude.Model,
                max_tokens = 8000, // Larger for section generation
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
                "Generating sections for toolbox talk {Id} with Claude AI (min sections: {MinSections})",
                toolboxTalkId, minimumSections);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Claude API error for toolbox talk {Id}: {StatusCode} - {Response}",
                    toolboxTalkId, response.StatusCode, responseBody);
                return new SectionGenerationResult(
                    Success: false,
                    Sections: new List<GeneratedSection>(),
                    ErrorMessage: $"Claude API error: {response.StatusCode}",
                    TokensUsed: 0);
            }

            var (sections, tokensUsed) = ParseSectionsFromResponse(responseBody, hasVideoContent, hasPdfContent);

            if (sections.Count < minimumSections)
            {
                _logger.LogWarning(
                    "AI generated only {Count} sections for toolbox talk {Id}, minimum was {Minimum}",
                    sections.Count, toolboxTalkId, minimumSections);
            }

            _logger.LogInformation(
                "Successfully generated {Count} sections for toolbox talk {Id} ({TokensUsed} tokens used)",
                sections.Count, toolboxTalkId, tokensUsed);

            return new SectionGenerationResult(
                Success: true,
                Sections: sections,
                ErrorMessage: null,
                TokensUsed: tokensUsed);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during section generation for toolbox talk {Id}", toolboxTalkId);
            return new SectionGenerationResult(
                Success: false,
                Sections: new List<GeneratedSection>(),
                ErrorMessage: $"HTTP request failed: {ex.Message}",
                TokensUsed: 0);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse section generation response for toolbox talk {Id}", toolboxTalkId);
            return new SectionGenerationResult(
                Success: false,
                Sections: new List<GeneratedSection>(),
                ErrorMessage: $"Failed to parse response: {ex.Message}",
                TokensUsed: 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Section generation failed for toolbox talk {Id}", toolboxTalkId);
            return new SectionGenerationResult(
                Success: false,
                Sections: new List<GeneratedSection>(),
                ErrorMessage: $"Section generation failed: {ex.Message}",
                TokensUsed: 0);
        }
    }

    /// <summary>
    /// Builds the prompt for section generation.
    /// </summary>
    private static string BuildSectionPrompt(
        string content,
        string sourceDescription,
        int minimumSections,
        bool hasVideo,
        bool hasPdf)
    {
        var sourceTracking = (hasVideo, hasPdf) switch
        {
            (true, true) => @"
For each section, indicate the source:
- ""Video"" if the information comes primarily from the video transcript
- ""Pdf"" if the information comes primarily from the PDF document
- ""Both"" if the information combines content from both sources",
            (true, false) => @"All sections should have source ""Video"" since content is from video transcript only.",
            (false, true) => @"All sections should have source ""Pdf"" since content is from PDF document only.",
            _ => ""
        };

        return $@"You are a workplace safety training expert. Analyze the following {sourceDescription} and create clear, concise sections that summarize the key safety points.

REQUIREMENTS:
- Create at least {minimumSections} sections (more if the content warrants it)
- Each section needs a clear, descriptive title
- Each section content should be 4-5 lines (a short paragraph)
- Focus on the most important safety information, procedures, and requirements
- Use clear, simple language suitable for all employees
- Sections should be logically ordered (general concepts first, then specific procedures)
{sourceTracking}

OUTPUT FORMAT:
Return your response as a JSON array with this exact structure:
```json
[
  {{
    ""sortOrder"": 1,
    ""title"": ""Section Title Here"",
    ""content"": ""The paragraph content here, 4-5 lines covering this key safety point."",
    ""source"": ""Video""
  }},
  {{
    ""sortOrder"": 2,
    ""title"": ""Another Section Title"",
    ""content"": ""Another paragraph covering a different key safety point."",
    ""source"": ""Pdf""
  }}
]
```

IMPORTANT: Return ONLY the JSON array, no additional text or explanation.

CONTENT TO ANALYZE:
{content}";
    }

    /// <summary>
    /// Parses the Claude API response to extract sections.
    /// </summary>
    private (List<GeneratedSection> sections, int tokensUsed) ParseSectionsFromResponse(
        string responseBody,
        bool hasVideo,
        bool hasPdf)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);

        // Extract token usage
        var tokensUsed = 0;
        if (jsonDoc.RootElement.TryGetProperty("usage", out var usageEl))
        {
            var inputTokens = usageEl.TryGetProperty("input_tokens", out var inputEl) ? inputEl.GetInt32() : 0;
            var outputTokens = usageEl.TryGetProperty("output_tokens", out var outputEl) ? outputEl.GetInt32() : 0;
            tokensUsed = inputTokens + outputTokens;
        }

        // Extract content
        if (!jsonDoc.RootElement.TryGetProperty("content", out var contentArray))
        {
            _logger.LogWarning("No content property found in Claude response");
            return (new List<GeneratedSection>(), tokensUsed);
        }

        string? responseText = null;
        foreach (var item in contentArray.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var textEl))
            {
                responseText = textEl.GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(responseText))
        {
            _logger.LogWarning("Empty text in Claude response");
            return (new List<GeneratedSection>(), tokensUsed);
        }

        // Extract JSON array from response (may have markdown code blocks)
        var jsonStart = responseText.IndexOf('[');
        var jsonEnd = responseText.LastIndexOf(']');

        if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
        {
            _logger.LogWarning("Could not find JSON array in AI response");
            return (new List<GeneratedSection>(), tokensUsed);
        }

        var jsonContent = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var rawSections = JsonSerializer.Deserialize<List<RawGeneratedSection>>(jsonContent, options);

        if (rawSections == null || rawSections.Count == 0)
        {
            _logger.LogWarning("No sections parsed from AI response");
            return (new List<GeneratedSection>(), tokensUsed);
        }

        // Convert raw sections to GeneratedSection with proper ContentSource enum
        var sections = rawSections
            .Select(s => new GeneratedSection(
                s.SortOrder,
                s.Title,
                s.Content,
                ParseContentSource(s.Source, hasVideo, hasPdf)))
            .ToList();

        return (sections, tokensUsed);
    }

    /// <summary>
    /// Parses the source string from AI response to ContentSource enum.
    /// </summary>
    private static ContentSource ParseContentSource(string? source, bool hasVideo, bool hasPdf)
    {
        // Default based on available sources if not specified
        if (string.IsNullOrWhiteSpace(source))
        {
            return (hasVideo, hasPdf) switch
            {
                (true, true) => ContentSource.Both,
                (true, false) => ContentSource.Video,
                (false, true) => ContentSource.Pdf,
                _ => ContentSource.Manual
            };
        }

        return source.ToLowerInvariant() switch
        {
            "video" => ContentSource.Video,
            "pdf" => ContentSource.Pdf,
            "both" => ContentSource.Both,
            _ => ContentSource.Manual
        };
    }

    /// <summary>
    /// Raw section data from JSON parsing (before enum conversion).
    /// </summary>
    private record RawGeneratedSection(
        int SortOrder,
        string Title,
        string Content,
        string? Source);
}
