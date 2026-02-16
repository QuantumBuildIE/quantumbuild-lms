using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// Simplified DTO for employee portal - shows assigned talk with translated content
/// </summary>
public record MyToolboxTalkDto
{
    public Guid ScheduledTalkId { get; init; }
    public Guid ToolboxTalkId { get; init; }

    // Talk details (translated if available)
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? VideoUrl { get; init; }
    public VideoSource VideoSource { get; init; }
    public string? AttachmentUrl { get; init; }
    public string? PdfUrl { get; init; }
    public string? PdfFileName { get; init; }
    public int MinimumVideoWatchPercent { get; init; }
    public bool RequiresQuiz { get; init; }
    public int? PassingScore { get; init; }

    // Assignment details
    public DateTime RequiredDate { get; init; }
    public DateTime DueDate { get; init; }
    public ScheduledTalkStatus Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = "en";

    /// <summary>
    /// The employee's preferred language for subtitle selection (e.g., "es", "pl", "ro")
    /// This is separate from LanguageCode which is used for content translation
    /// </summary>
    public string EmployeePreferredLanguage { get; init; } = "en";

    // Progress tracking
    public int TotalSections { get; init; }
    public int CompletedSections { get; init; }
    public decimal ProgressPercent { get; init; }
    public int? VideoWatchPercent { get; init; }

    // Quiz status
    public int QuizAttemptCount { get; init; }
    public bool? LastQuizPassed { get; init; }
    public decimal? LastQuizScore { get; init; }

    // Slideshow
    public bool HasSlideshow { get; init; }

    // Sections with progress
    public List<MyToolboxTalkSectionDto> Sections { get; init; } = new();

    // Questions (without correct answers for quiz taking)
    public List<MyToolboxTalkQuestionDto> Questions { get; init; } = new();

    // Completion details (if completed)
    public DateTime? CompletedAt { get; init; }
    public string? CertificateUrl { get; init; }

    // Timing
    public bool IsOverdue { get; init; }
    public int DaysUntilDue { get; init; }
}

/// <summary>
/// Question DTO for employees taking the quiz (no correct answer exposed)
/// </summary>
public record MyToolboxTalkQuestionDto
{
    public Guid Id { get; init; }
    public int QuestionNumber { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public QuestionType QuestionType { get; init; }
    public string QuestionTypeDisplay { get; init; } = string.Empty;
    public List<string>? Options { get; init; }
    public int Points { get; init; }

    /// <summary>
    /// Maps each display position to its original option index.
    /// Only populated for shuffled quizzes. For non-shuffled quizzes this is null
    /// (display index equals original index).
    /// Example: [2,0,3,1] means display position 0 shows original option 2.
    /// Frontend uses this to send the original index when submitting answers.
    /// </summary>
    public List<int>? OptionOriginalIndices { get; init; }
}
