using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Prompts;
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

            var prompt = QuizGenerationPrompts.BuildQuizPrompt(
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
            var prompt = QuizGenerationPrompts.BuildFinalPortionQuestionPrompt(finalPortionContent, sortOrder);

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
