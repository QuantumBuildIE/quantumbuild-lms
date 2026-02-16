using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// AI-powered quiz question generation service using Claude (Anthropic) API.
/// Generates multiple-choice questions from video transcripts and/or PDF content.
/// </summary>
public class AiQuizGenerationService : IAiQuizGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<AiQuizGenerationService> _logger;

    public AiQuizGenerationService(
        HttpClient httpClient,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<AiQuizGenerationService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<QuizGenerationResult> GenerateQuizAsync(
        Guid toolboxTalkId,
        string combinedContent,
        string? videoFinalPortionContent,
        bool hasVideoContent,
        bool hasPdfContent,
        int minimumQuestions = 5,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(_settings.Claude.ApiKey))
            {
                _logger.LogError("Claude API key is not configured");
                return new QuizGenerationResult(
                    Success: false,
                    Questions: new List<GeneratedQuizQuestion>(),
                    ErrorMessage: "Claude API key not configured",
                    TokensUsed: 0,
                    HasFinalPortionQuestion: false);
            }

            if (string.IsNullOrWhiteSpace(combinedContent))
            {
                _logger.LogWarning("No content provided for quiz generation for toolbox talk {Id}", toolboxTalkId);
                return new QuizGenerationResult(
                    Success: false,
                    Questions: new List<GeneratedQuizQuestion>(),
                    ErrorMessage: "No content provided for quiz generation",
                    TokensUsed: 0,
                    HasFinalPortionQuestion: false);
            }

            var prompt = BuildQuizPrompt(
                combinedContent,
                videoFinalPortionContent,
                hasVideoContent,
                hasPdfContent,
                minimumQuestions);

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

            _logger.LogInformation(
                "Generating quiz questions for toolbox talk {Id} with Claude AI (min questions: {MinQuestions})",
                toolboxTalkId, minimumQuestions);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Claude API error for toolbox talk {Id}: {StatusCode} - {Response}",
                    toolboxTalkId, response.StatusCode, responseBody);
                return new QuizGenerationResult(
                    Success: false,
                    Questions: new List<GeneratedQuizQuestion>(),
                    ErrorMessage: $"Claude API error: {response.StatusCode}",
                    TokensUsed: 0,
                    HasFinalPortionQuestion: false);
            }

            var (questions, tokensUsed) = ParseQuestionsFromResponse(responseBody, hasVideoContent, hasPdfContent);

            // Validate we have at least one final portion question if video was included
            var hasFinalPortionQuestion = questions.Any(q => q.IsFromVideoFinalPortion);

            if (hasVideoContent && !string.IsNullOrEmpty(videoFinalPortionContent) && !hasFinalPortionQuestion)
            {
                _logger.LogWarning(
                    "AI did not generate a question from video final portion for toolbox talk {Id}. Requesting additional question.",
                    toolboxTalkId);

                // Request a specific final portion question
                var additionalResult = await GenerateFinalPortionQuestionAsync(
                    toolboxTalkId, videoFinalPortionContent, questions.Count + 1, cancellationToken);

                if (additionalResult.question != null)
                {
                    questions.Add(additionalResult.question);
                    tokensUsed += additionalResult.tokensUsed;
                    hasFinalPortionQuestion = true;
                }
            }

            if (questions.Count < minimumQuestions)
            {
                _logger.LogWarning(
                    "AI generated only {Count} questions for toolbox talk {Id}, minimum was {Minimum}",
                    questions.Count, toolboxTalkId, minimumQuestions);
            }

            _logger.LogInformation(
                "Successfully generated {Count} quiz questions for toolbox talk {Id} ({TokensUsed} tokens used, final portion: {HasFinal})",
                questions.Count, toolboxTalkId, tokensUsed, hasFinalPortionQuestion);

            return new QuizGenerationResult(
                Success: true,
                Questions: questions,
                ErrorMessage: null,
                TokensUsed: tokensUsed,
                HasFinalPortionQuestion: hasFinalPortionQuestion);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed during quiz generation for toolbox talk {Id}", toolboxTalkId);
            return new QuizGenerationResult(
                Success: false,
                Questions: new List<GeneratedQuizQuestion>(),
                ErrorMessage: $"HTTP request failed: {ex.Message}",
                TokensUsed: 0,
                HasFinalPortionQuestion: false);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse quiz generation response for toolbox talk {Id}", toolboxTalkId);
            return new QuizGenerationResult(
                Success: false,
                Questions: new List<GeneratedQuizQuestion>(),
                ErrorMessage: $"Failed to parse response: {ex.Message}",
                TokensUsed: 0,
                HasFinalPortionQuestion: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Quiz generation failed for toolbox talk {Id}", toolboxTalkId);
            return new QuizGenerationResult(
                Success: false,
                Questions: new List<GeneratedQuizQuestion>(),
                ErrorMessage: $"Quiz generation failed: {ex.Message}",
                TokensUsed: 0,
                HasFinalPortionQuestion: false);
        }
    }

    /// <summary>
    /// Generates a single question specifically from the final portion of the video.
    /// </summary>
    private async Task<(GeneratedQuizQuestion? question, int tokensUsed)> GenerateFinalPortionQuestionAsync(
        Guid toolboxTalkId,
        string finalPortionContent,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        try
        {
            var prompt = $@"You are a workplace safety training expert. Create ONE multiple-choice quiz question based ONLY on the following content from the final portion of a training video.

This question is specifically to verify the employee watched the entire video.

CONTENT FROM VIDEO FINAL PORTION (80-100%):
{finalPortionContent}

REQUIREMENTS:
- Create exactly ONE question
- The question must have exactly 4 options (A, B, C, D)
- Only ONE option should be correct
- The question should test important safety knowledge from this portion
- Use clear, unambiguous language

OUTPUT FORMAT:
Return your response as a JSON object with this exact structure:
```json
{{
  ""sortOrder"": {sortOrder},
  ""questionText"": ""Your question here?"",
  ""options"": [""Option A"", ""Option B"", ""Option C"", ""Option D""],
  ""correctAnswerIndex"": 0,
  ""source"": ""Video"",
  ""isFromVideoFinalPortion"": true,
  ""videoTimestamp"": ""final portion""
}}
```

Note: correctAnswerIndex is 0-based (0 = first option, 3 = fourth option)

IMPORTANT: Return ONLY the JSON object, no additional text or explanation.

Generate the question now:";

            var requestBody = new
            {
                model = _settings.Claude.Model,
                max_tokens = 2000,
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
                    "Claude API error when generating final portion question for toolbox talk {Id}: {StatusCode}",
                    toolboxTalkId, response.StatusCode);
                return (null, 0);
            }

            return ParseSingleQuestionFromResponse(responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate final portion question for toolbox talk {Id}", toolboxTalkId);
            return (null, 0);
        }
    }

    /// <summary>
    /// Builds the prompt for quiz generation.
    /// </summary>
    private static string BuildQuizPrompt(
        string content,
        string? videoFinalPortionContent,
        bool hasVideo,
        bool hasPdf,
        int minimumQuestions)
    {
        var sourceGuidance = (hasVideo, hasPdf) switch
        {
            (true, true) => @"
- Create questions from BOTH the video transcript AND the PDF document
- Mark each question with its source (""Video"" or ""Pdf"")
- Aim for a balanced mix of questions from both sources",
            (true, false) => @"
- All questions should come from the video transcript
- Mark all questions with source ""Video""",
            (false, true) => @"
- All questions should come from the PDF document
- Mark all questions with source ""Pdf""",
            _ => ""
        };

        var finalPortionRequirement = "";
        if (hasVideo && !string.IsNullOrEmpty(videoFinalPortionContent))
        {
            finalPortionRequirement = $@"

CRITICAL REQUIREMENT - FINAL PORTION QUESTION:
At least ONE question MUST be based on content from the VIDEO FINAL PORTION section below.
This ensures employees watched the entire video. Mark this question with ""isFromVideoFinalPortion"": true.

VIDEO FINAL PORTION CONTENT (80-100% of video):
{videoFinalPortionContent}
--- END OF FINAL PORTION ---";
        }

        var maxQuestions = Math.Max(10, minimumQuestions + 5);

        return $@"You are a workplace safety training expert. Create multiple-choice quiz questions to test employee understanding of the following safety training content.

REQUIREMENTS:
- Create at least {minimumQuestions} questions (up to {maxQuestions} for longer content)
- Each question must have exactly 4 options (A, B, C, D)
- Only ONE option should be correct
- Questions should test important safety knowledge, not trivial details
- Use clear, unambiguous language
- Options should be plausible (avoid obviously wrong answers)
{sourceGuidance}
{finalPortionRequirement}

OUTPUT FORMAT:
Return your response as a JSON array with this exact structure:
```json
[
  {{
    ""sortOrder"": 1,
    ""questionText"": ""What is the correct procedure for...?"",
    ""options"": [""Option A text"", ""Option B text"", ""Option C text"", ""Option D text""],
    ""correctAnswerIndex"": 2,
    ""source"": ""Video"",
    ""isFromVideoFinalPortion"": false,
    ""videoTimestamp"": ""2:30""
  }},
  {{
    ""sortOrder"": 2,
    ""questionText"": ""According to the safety guidelines...?"",
    ""options"": [""Option A"", ""Option B"", ""Option C"", ""Option D""],
    ""correctAnswerIndex"": 0,
    ""source"": ""Pdf"",
    ""isFromVideoFinalPortion"": false,
    ""videoTimestamp"": null
  }}
]
```

Note: correctAnswerIndex is 0-based (0 = first option, 3 = fourth option)

IMPORTANT: Return ONLY the JSON array, no additional text or explanation.

CONTENT TO ANALYZE:
{content}

Generate the quiz questions now as a JSON array:";
    }

    /// <summary>
    /// Parses the Claude API response to extract quiz questions.
    /// </summary>
    private (List<GeneratedQuizQuestion> questions, int tokensUsed) ParseQuestionsFromResponse(
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
            return (new List<GeneratedQuizQuestion>(), tokensUsed);
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
            return (new List<GeneratedQuizQuestion>(), tokensUsed);
        }

        // Extract JSON array from response (may have markdown code blocks)
        var jsonStart = responseText.IndexOf('[');
        var jsonEnd = responseText.LastIndexOf(']');

        if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
        {
            _logger.LogWarning("Could not find JSON array in AI quiz response");
            return (new List<GeneratedQuizQuestion>(), tokensUsed);
        }

        var jsonContent = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var rawQuestions = JsonSerializer.Deserialize<List<RawGeneratedQuestion>>(jsonContent, options);

        if (rawQuestions == null || rawQuestions.Count == 0)
        {
            _logger.LogWarning("No questions parsed from AI response");
            return (new List<GeneratedQuizQuestion>(), tokensUsed);
        }

        // Convert raw questions to GeneratedQuizQuestion with proper ContentSource enum
        var questions = rawQuestions
            .Select(q => new GeneratedQuizQuestion(
                q.SortOrder,
                q.QuestionText,
                q.Options ?? new List<string>(),
                q.CorrectAnswerIndex,
                ParseContentSource(q.Source, hasVideo, hasPdf),
                q.IsFromVideoFinalPortion,
                q.VideoTimestamp))
            .ToList();

        return (questions, tokensUsed);
    }

    /// <summary>
    /// Parses a single question from the Claude API response.
    /// </summary>
    private (GeneratedQuizQuestion? question, int tokensUsed) ParseSingleQuestionFromResponse(string responseBody)
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
            _logger.LogWarning("No content property found in Claude response for single question");
            return (null, tokensUsed);
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
            return (null, tokensUsed);
        }

        // Extract JSON object from response
        var jsonStart = responseText.IndexOf('{');
        var jsonEnd = responseText.LastIndexOf('}');

        if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
        {
            _logger.LogWarning("Could not find JSON object in AI single question response");
            return (null, tokensUsed);
        }

        var jsonContent = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var rawQuestion = JsonSerializer.Deserialize<RawGeneratedQuestion>(jsonContent, options);

        if (rawQuestion == null)
        {
            return (null, tokensUsed);
        }

        var question = new GeneratedQuizQuestion(
            rawQuestion.SortOrder,
            rawQuestion.QuestionText,
            rawQuestion.Options ?? new List<string>(),
            rawQuestion.CorrectAnswerIndex,
            ContentSource.Video,
            rawQuestion.IsFromVideoFinalPortion,
            rawQuestion.VideoTimestamp);

        return (question, tokensUsed);
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
    /// Raw question data from JSON parsing (before enum conversion).
    /// </summary>
    private record RawGeneratedQuestion(
        int SortOrder,
        string QuestionText,
        List<string>? Options,
        int CorrectAnswerIndex,
        string? Source,
        bool IsFromVideoFinalPortion,
        string? VideoTimestamp);
}
