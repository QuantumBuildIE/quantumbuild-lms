namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for a quiz attempt within a scheduled talk
/// </summary>
public record ScheduledTalkQuizAttemptDto
{
    public Guid Id { get; init; }
    public Guid ScheduledTalkId { get; init; }
    public int AttemptNumber { get; init; }

    /// <summary>
    /// JSON string containing question ID to answer mapping
    /// </summary>
    public string Answers { get; init; } = "{}";

    public int Score { get; init; }
    public int MaxScore { get; init; }
    public decimal Percentage { get; init; }
    public bool Passed { get; init; }
    public DateTime AttemptedAt { get; init; }
}
