using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Text.Json;

namespace QuantumBuild.Tests.Common.Builders;

/// <summary>
/// Fluent builder for creating ToolboxTalk entities in tests.
/// </summary>
public class ToolboxTalkBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _tenantId = TestTenant.TestTenantConstants.TenantId;
    private string _title = "Test Toolbox Talk";
    private string? _description = "Test description";
    private ToolboxTalkFrequency _frequency = ToolboxTalkFrequency.Once;
    private string? _videoUrl = null;
    private VideoSource _videoSource = VideoSource.None;
    private int _minimumVideoWatchPercent = 90;
    private bool _requiresQuiz = false;
    private int? _passingScore = null;
    private bool _isActive = true;
    private readonly List<ToolboxTalkSection> _sections = new();
    private readonly List<ToolboxTalkQuestion> _questions = new();

    public ToolboxTalkBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ToolboxTalkBuilder WithTenantId(Guid tenantId)
    {
        _tenantId = tenantId;
        return this;
    }

    public ToolboxTalkBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    public ToolboxTalkBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public ToolboxTalkBuilder WithFrequency(ToolboxTalkFrequency frequency)
    {
        _frequency = frequency;
        return this;
    }

    public ToolboxTalkBuilder WithVideo(string url, VideoSource source = VideoSource.YouTube, int minimumWatchPercent = 90)
    {
        _videoUrl = url;
        _videoSource = source;
        _minimumVideoWatchPercent = minimumWatchPercent;
        return this;
    }

    public ToolboxTalkBuilder WithQuiz(int passingScore = 80)
    {
        _requiresQuiz = true;
        _passingScore = passingScore;
        return this;
    }

    public ToolboxTalkBuilder WithoutQuiz()
    {
        _requiresQuiz = false;
        _passingScore = null;
        return this;
    }

    public ToolboxTalkBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public ToolboxTalkBuilder WithSection(string title, string content, bool requiresAcknowledgment = true)
    {
        _sections.Add(new ToolboxTalkSection
        {
            Id = Guid.NewGuid(),
            ToolboxTalkId = _id,
            SectionNumber = _sections.Count + 1,
            Title = title,
            Content = content,
            RequiresAcknowledgment = requiresAcknowledgment,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-builder"
        });
        return this;
    }

    public ToolboxTalkBuilder WithMultipleChoiceQuestion(string questionText, string[] options, string correctAnswer, int points = 1)
    {
        _questions.Add(new ToolboxTalkQuestion
        {
            Id = Guid.NewGuid(),
            ToolboxTalkId = _id,
            QuestionNumber = _questions.Count + 1,
            QuestionText = questionText,
            QuestionType = QuestionType.MultipleChoice,
            Options = JsonSerializer.Serialize(options),
            CorrectAnswer = correctAnswer,
            Points = points,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-builder"
        });
        return this;
    }

    public ToolboxTalkBuilder WithTrueFalseQuestion(string questionText, bool correctAnswer, int points = 1)
    {
        _questions.Add(new ToolboxTalkQuestion
        {
            Id = Guid.NewGuid(),
            ToolboxTalkId = _id,
            QuestionNumber = _questions.Count + 1,
            QuestionText = questionText,
            QuestionType = QuestionType.TrueFalse,
            CorrectAnswer = correctAnswer ? "True" : "False",
            Points = points,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-builder"
        });
        return this;
    }

    public ToolboxTalkBuilder WithShortAnswerQuestion(string questionText, string correctAnswer, int points = 1)
    {
        _questions.Add(new ToolboxTalkQuestion
        {
            Id = Guid.NewGuid(),
            ToolboxTalkId = _id,
            QuestionNumber = _questions.Count + 1,
            QuestionText = questionText,
            QuestionType = QuestionType.ShortAnswer,
            CorrectAnswer = correctAnswer,
            Points = points,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-builder"
        });
        return this;
    }

    public ToolboxTalk Build()
    {
        var talk = new ToolboxTalk
        {
            Id = _id,
            TenantId = _tenantId,
            Title = _title,
            Description = _description,
            Frequency = _frequency,
            VideoUrl = _videoUrl,
            VideoSource = _videoSource,
            MinimumVideoWatchPercent = _minimumVideoWatchPercent,
            RequiresQuiz = _requiresQuiz,
            PassingScore = _passingScore,
            IsActive = _isActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-builder"
        };

        foreach (var section in _sections)
        {
            section.ToolboxTalkId = _id;
            talk.Sections.Add(section);
        }

        foreach (var question in _questions)
        {
            question.ToolboxTalkId = _id;
            talk.Questions.Add(question);
        }

        return talk;
    }

    /// <summary>
    /// Creates a basic talk with default sections.
    /// </summary>
    public static ToolboxTalk CreateBasicTalk(Guid? id = null, string title = "Basic Test Talk")
    {
        return new ToolboxTalkBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithTitle(title)
            .WithSection("Introduction", "<p>Introduction content</p>")
            .WithSection("Main Content", "<p>Main content</p>")
            .Build();
    }

    /// <summary>
    /// Creates a talk with quiz.
    /// </summary>
    public static ToolboxTalk CreateTalkWithQuiz(Guid? id = null, string title = "Quiz Test Talk", int passingScore = 80)
    {
        return new ToolboxTalkBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithTitle(title)
            .WithQuiz(passingScore)
            .WithSection("Safety Overview", "<p>Safety content to be tested</p>")
            .WithMultipleChoiceQuestion(
                "What is the correct answer?",
                new[] { "Option A", "Option B", "Option C", "Option D" },
                "Option A")
            .WithTrueFalseQuestion("Safety is important.", true)
            .Build();
    }

    /// <summary>
    /// Creates a talk with video.
    /// </summary>
    public static ToolboxTalk CreateTalkWithVideo(Guid? id = null, string title = "Video Test Talk", string videoUrl = "https://youtube.com/watch?v=test")
    {
        return new ToolboxTalkBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithTitle(title)
            .WithVideo(videoUrl, VideoSource.YouTube, 90)
            .WithQuiz(70)
            .WithSection("Video Introduction", "<p>Watch the video and answer questions</p>")
            .WithMultipleChoiceQuestion(
                "What was shown in the video?",
                new[] { "Safety procedures", "Cooking recipes", "Sports", "Music" },
                "Safety procedures")
            .Build();
    }
}
