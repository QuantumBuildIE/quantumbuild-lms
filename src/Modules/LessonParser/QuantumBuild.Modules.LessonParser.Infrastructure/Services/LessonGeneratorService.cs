using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Abstractions.AI;
using QuantumBuild.Modules.LessonParser.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Services;

/// <summary>
/// Generates ToolboxTalk entities and a Course from extracted document content
/// by calling Claude AI to parse the content into structured topics.
/// </summary>
public class LessonGeneratorService : ILessonGeneratorService
{
    private readonly HttpClient _httpClient;
    private readonly ClaudeSettings _claudeSettings;
    private readonly IToolboxTalksDbContext _toolboxTalksDbContext;
    private readonly ILogger<LessonGeneratorService> _logger;

    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "for", "in", "to", "of", "on", "at", "by", "with", "from", "is", "it"
    };

    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LessonGeneratorService(
        HttpClient httpClient,
        IOptions<ClaudeSettings> claudeSettings,
        IToolboxTalksDbContext toolboxTalksDbContext,
        ILogger<LessonGeneratorService> logger)
    {
        _httpClient = httpClient;
        _claudeSettings = claudeSettings.Value;
        _toolboxTalksDbContext = toolboxTalksDbContext;
        _logger = logger;
    }

    public async Task<LessonParseResult> GenerateFromContentAsync(
        ExtractionResult extractedContent,
        Guid tenantId,
        string createdBy,
        IProgress<LessonParseProgress> progress,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Call Claude API to parse content into topics
        var content = extractedContent.Content;
        if (content.Length > 100_000)
        {
            content = content[..100_000] + "\n\n[Content truncated for processing]";
        }

        var prompt = BuildPrompt(extractedContent.Title, content);

        _logger.LogInformation(
            "Calling Claude API to parse content for tenant {TenantId}, title: {Title}",
            tenantId, extractedContent.Title);

        var responseText = await CallClaudeApiAsync(prompt, cancellationToken);

        // Parse JSON response into ParsedCourse
        ParsedCourse parsedCourse;
        try
        {
            parsedCourse = ParseCourseFromResponse(responseText);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Claude response as JSON. Raw response: {Response}", responseText);
            throw new InvalidOperationException(
                "AI returned an unexpected response format. Please try again.");
        }

        if (parsedCourse.Topics.Count == 0)
        {
            throw new InvalidOperationException(
                "AI did not identify any topics from the provided content. Please try again with different content.");
        }

        progress.Report(new LessonParseProgress
        {
            Stage = "Topics identified",
            PercentComplete = 10,
            TotalTalks = parsedCourse.Topics.Count
        });

        // Step 2: Generate ToolboxTalk per topic
        var totalTopics = parsedCourse.Topics.Count;
        var generatedTalks = new List<ToolboxTalk>();

        for (var i = 0; i < totalTopics; i++)
        {
            var topic = parsedCourse.Topics[i];

            // Generate unique code using the same pattern as CreateToolboxTalkCommandHandler
            var code = await GenerateCodeAsync(topic.Title, tenantId, cancellationToken);

            // Create ToolboxTalk entity
            var toolboxTalk = new ToolboxTalk
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Title = topic.Title,
                Description = $"Auto-generated from: {extractedContent.Title}",
                Status = ToolboxTalkStatus.Published,
                IsActive = true,
                Frequency = ToolboxTalkFrequency.Once,
                VideoSource = VideoSource.None,
                MinimumVideoWatchPercent = 90,
                RequiresQuiz = topic.Questions.Count > 0,
                PassingScore = topic.Questions.Count > 0 ? 80 : null,
                SourceLanguageCode = "en",
                CreatedBy = createdBy
            };

            // Create sections
            for (var s = 0; s < topic.Sections.Count; s++)
            {
                var section = topic.Sections[s];
                toolboxTalk.Sections.Add(new ToolboxTalkSection
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = toolboxTalk.Id,
                    SectionNumber = s + 1,
                    Title = section.Title,
                    Content = section.Content,
                    RequiresAcknowledgment = true,
                    Source = ContentSource.Manual
                });
            }

            // Create questions (Options stored as JSON string)
            for (var q = 0; q < topic.Questions.Count; q++)
            {
                var question = topic.Questions[q];
                var correctAnswer = question.CorrectOptionIndex >= 0 && question.CorrectOptionIndex < question.Options.Count
                    ? question.Options[question.CorrectOptionIndex]
                    : question.Options.FirstOrDefault() ?? string.Empty;

                toolboxTalk.Questions.Add(new ToolboxTalkQuestion
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = toolboxTalk.Id,
                    QuestionNumber = q + 1,
                    QuestionText = question.Question,
                    QuestionType = QuestionType.MultipleChoice,
                    Options = JsonSerializer.Serialize(question.Options),
                    CorrectAnswer = correctAnswer,
                    CorrectOptionIndex = question.CorrectOptionIndex,
                    Points = 1,
                    Source = ContentSource.Manual
                });
            }

            // Save after each talk to preserve partial progress
            _toolboxTalksDbContext.ToolboxTalks.Add(toolboxTalk);
            await _toolboxTalksDbContext.SaveChangesAsync(cancellationToken);

            generatedTalks.Add(toolboxTalk);

            _logger.LogInformation(
                "Created talk {TalkNumber}/{Total}: {Code} - {Title} ({SectionCount} sections, {QuestionCount} questions)",
                i + 1, totalTopics, code, topic.Title, topic.Sections.Count, topic.Questions.Count);

            progress.Report(new LessonParseProgress
            {
                Stage = $"Created talk: {topic.Title}",
                PercentComplete = 10 + (int)((double)(i + 1) / totalTopics * 80),
                CurrentTalk = i + 1,
                TotalTalks = totalTopics
            });
        }

        // Step 3: Create ToolboxTalkCourse
        var course = new ToolboxTalkCourse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = parsedCourse.CourseTitle,
            Description = $"Auto-generated from: {extractedContent.Title}",
            IsActive = true,
            RequireSequentialCompletion = true,
            CreatedBy = createdBy
        };

        // Create course items linking each talk in order
        for (var i = 0; i < generatedTalks.Count; i++)
        {
            course.CourseItems.Add(new ToolboxTalkCourseItem
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                ToolboxTalkId = generatedTalks[i].Id,
                OrderIndex = i,
                IsRequired = true
            });
        }

        _toolboxTalksDbContext.ToolboxTalkCourses.Add(course);
        await _toolboxTalksDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created course {CourseId}: {CourseTitle} with {TalkCount} talks for tenant {TenantId}",
            course.Id, course.Title, generatedTalks.Count, tenantId);

        progress.Report(new LessonParseProgress
        {
            Stage = "Course created",
            PercentComplete = 100,
            CurrentTalk = totalTopics,
            TotalTalks = totalTopics
        });

        return new LessonParseResult
        {
            CourseId = course.Id,
            CourseTitle = course.Title,
            TalksGenerated = generatedTalks.Count
        };
    }

    /// <summary>
    /// Calls the Claude API following the exact pattern used in ToolboxTalks services.
    /// </summary>
    private async Task<string> CallClaudeApiAsync(string prompt, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_claudeSettings.ApiKey))
        {
            throw new InvalidOperationException("Claude API key is not configured");
        }

        var requestBody = new
        {
            model = _claudeSettings.Model,
            max_tokens = 16000,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_claudeSettings.BaseUrl}/messages");
        request.Headers.Add("x-api-key", _claudeSettings.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Claude API error: {StatusCode} - {Response}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"Claude API error: {response.StatusCode}");
        }

        // Extract text from Claude response using the standard pattern
        return ParseClaudeResponseText(responseBody);
    }

    /// <summary>
    /// Extracts the text content from a Claude API response.
    /// </summary>
    private static string ParseClaudeResponseText(string responseBody)
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

    /// <summary>
    /// Parses the Claude response text into a ParsedCourse object.
    /// Handles responses that may include markdown code blocks around JSON.
    /// </summary>
    private static ParsedCourse ParseCourseFromResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            throw new JsonException("Empty response from Claude API");
        }

        // Try to extract JSON object from the response (may be wrapped in markdown code blocks)
        var jsonStart = responseText.IndexOf('{');
        var jsonEnd = responseText.LastIndexOf('}');

        if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
        {
            throw new JsonException("Could not find JSON object in AI response");
        }

        var jsonContent = responseText.Substring(jsonStart, jsonEnd - jsonStart + 1);

        var parsed = JsonSerializer.Deserialize<ParsedCourse>(jsonContent, CaseInsensitiveOptions);

        return parsed ?? throw new JsonException("Deserialized ParsedCourse was null");
    }

    /// <summary>
    /// Generates a unique code for a toolbox talk using the same algorithm
    /// as CreateToolboxTalkCommandHandler: initials from title + numeric suffix.
    /// </summary>
    private async Task<string> GenerateCodeAsync(string title, Guid tenantId, CancellationToken cancellationToken)
    {
        // Strip common words and take first letter of each remaining word
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !CommonWords.Contains(w))
            .ToList();

        string prefix;
        if (words.Count >= 2)
        {
            prefix = string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
        }
        else
        {
            // Fewer than 2 meaningful words — take first 3 chars of title
            var cleaned = new string(title.Where(c => !char.IsWhiteSpace(c)).ToArray());
            prefix = cleaned.Length >= 3
                ? cleaned[..3].ToUpperInvariant()
                : cleaned.ToUpperInvariant();
        }

        // Truncate prefix if it would exceed max length with suffix (20 - 4 for "-NNN")
        if (prefix.Length > 16)
            prefix = prefix[..16];

        // Find existing codes with the same prefix to determine next number
        var pattern = prefix + "-";
        var existingCodes = await _toolboxTalksDbContext.ToolboxTalks
            .Where(t => t.TenantId == tenantId && t.Code.StartsWith(pattern))
            .Select(t => t.Code)
            .ToListAsync(cancellationToken);

        var maxNumber = 0;
        foreach (var existingCode in existingCodes)
        {
            var suffix = existingCode[pattern.Length..];
            if (int.TryParse(suffix, out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"{prefix}-{(maxNumber + 1):D3}";
    }

    /// <summary>
    /// Builds the Claude API prompt for parsing document content into a structured course.
    /// </summary>
    private static string BuildPrompt(string documentTitle, string content)
    {
        return $$"""
            You are a workplace safety training expert. Analyze the following document content and break it into distinct training topics (toolbox talks) for a workplace safety course.

            For the document titled "{{documentTitle}}", create a structured course with the following requirements:

            1. Break the content into 3-10 distinct training topics (each becomes a separate toolbox talk)
            2. Each topic should cover a single coherent subject area
            3. For each topic, create:
               - A clear, descriptive title (max 200 characters)
               - 3-7 content sections with educational HTML content
               - 3-5 multiple choice quiz questions with exactly 4 options each

            Section content requirements:
            - Use HTML formatting: <p> for paragraphs, <ul>/<ol> for lists, <strong> for emphasis, <h3> for sub-headings
            - Be educational, clear, and comprehensive
            - Cover the key points from the source material relevant to each topic

            Question requirements:
            - Test comprehension of the section content for that topic
            - Have exactly 4 options
            - Have exactly one correct answer
            - correctOptionIndex is 0-based (0 = first option)

            Return ONLY a JSON object with this exact structure (no markdown code blocks, no explanation):
            {
              "courseTitle": "Course title here",
              "topics": [
                {
                  "title": "Topic 1 Title",
                  "sections": [
                    {
                      "title": "Section Title",
                      "content": "<p>HTML content here</p>"
                    }
                  ],
                  "questions": [
                    {
                      "question": "Question text?",
                      "options": ["Option A", "Option B", "Option C", "Option D"],
                      "correctOptionIndex": 0
                    }
                  ]
                }
              ]
            }

            Document content:
            {{content}}
            """;
    }
}
