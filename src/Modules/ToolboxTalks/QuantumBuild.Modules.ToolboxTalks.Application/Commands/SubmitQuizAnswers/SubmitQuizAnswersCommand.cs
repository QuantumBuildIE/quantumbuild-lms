using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.SubmitQuizAnswers;

/// <summary>
/// Command to submit quiz answers for a scheduled toolbox talk.
/// </summary>
public record SubmitQuizAnswersCommand : IRequest<QuizResultDto>
{
    /// <summary>
    /// The scheduled talk for which the quiz is being submitted
    /// </summary>
    public Guid ScheduledTalkId { get; init; }

    /// <summary>
    /// Dictionary of question ID to submitted answer
    /// </summary>
    public Dictionary<Guid, string> Answers { get; init; } = new();
}

/// <summary>
/// Result of submitting quiz answers
/// </summary>
public record QuizResultDto
{
    /// <summary>
    /// Quiz attempt ID
    /// </summary>
    public Guid AttemptId { get; init; }

    /// <summary>
    /// Attempt number (1, 2, 3, etc.)
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// Number of correct answers
    /// </summary>
    public int Score { get; init; }

    /// <summary>
    /// Maximum possible score
    /// </summary>
    public int MaxScore { get; init; }

    /// <summary>
    /// Percentage score (0-100)
    /// </summary>
    public decimal Percentage { get; init; }

    /// <summary>
    /// Whether the quiz was passed
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Required passing score percentage
    /// </summary>
    public int PassingScore { get; init; }

    /// <summary>
    /// Details for each question showing if the answer was correct
    /// </summary>
    public List<QuestionResultDto> QuestionResults { get; init; } = new();
}

/// <summary>
/// Result for an individual question
/// </summary>
public record QuestionResultDto
{
    /// <summary>
    /// Question ID
    /// </summary>
    public Guid QuestionId { get; init; }

    /// <summary>
    /// Question number (display order)
    /// </summary>
    public int QuestionNumber { get; init; }

    /// <summary>
    /// The question text
    /// </summary>
    public string QuestionText { get; init; } = string.Empty;

    /// <summary>
    /// The answer submitted by the employee
    /// </summary>
    public string SubmittedAnswer { get; init; } = string.Empty;

    /// <summary>
    /// Whether the answer was correct
    /// </summary>
    public bool IsCorrect { get; init; }

    /// <summary>
    /// The correct answer text (shown after submission).
    /// For MultipleChoice, this is the English text. Use CorrectOptionIndex
    /// to identify the correct option in the displayed (possibly translated) options.
    /// </summary>
    public string CorrectAnswer { get; init; } = string.Empty;

    /// <summary>
    /// Index of the correct option in the original Options array (0-based).
    /// Used by the frontend to highlight the correct translated option.
    /// Null for non-MultipleChoice questions.
    /// </summary>
    public int? CorrectOptionIndex { get; init; }

    /// <summary>
    /// Points earned for this question
    /// </summary>
    public int PointsEarned { get; init; }

    /// <summary>
    /// Maximum points available for this question
    /// </summary>
    public int MaxPoints { get; init; }
}
