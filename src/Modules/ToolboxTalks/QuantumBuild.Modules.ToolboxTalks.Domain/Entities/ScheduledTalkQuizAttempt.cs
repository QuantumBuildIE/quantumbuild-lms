using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Records an employee's quiz attempt for a scheduled toolbox talk.
/// Stores answers, scores, and pass/fail status for each attempt.
/// </summary>
public class ScheduledTalkQuizAttempt : BaseEntity
{
    /// <summary>
    /// The scheduled talk this attempt belongs to
    /// </summary>
    public Guid ScheduledTalkId { get; set; }

    /// <summary>
    /// Attempt number (1, 2, 3, etc.) for multiple attempts
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// JSON string containing question ID to answer mapping
    /// Format: { "questionId1": "selectedAnswer", "questionId2": ["answer1", "answer2"] }
    /// </summary>
    public string Answers { get; set; } = "{}";

    /// <summary>
    /// Number of correct answers
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Maximum possible score (total questions)
    /// </summary>
    public int MaxScore { get; set; }

    /// <summary>
    /// Percentage score (Score / MaxScore * 100)
    /// </summary>
    public decimal Percentage { get; set; }

    /// <summary>
    /// Whether this attempt passed the required threshold
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// When the quiz attempt was submitted
    /// </summary>
    public DateTime AttemptedAt { get; set; }

    /// <summary>
    /// JSON storing the generated quiz configuration for this attempt.
    /// Contains question IDs, display order, and shuffled option indices.
    /// Used to reconstruct the exact quiz the employee saw.
    /// </summary>
    public string? GeneratedQuestionsJson { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent scheduled talk
    /// </summary>
    public ScheduledTalk ScheduledTalk { get; set; } = null!;
}
