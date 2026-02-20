namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;

public record CourseScheduledTalkDto
{
    public Guid ScheduledTalkId { get; init; }
    public Guid ToolboxTalkId { get; init; }
    public string TalkCode { get; init; } = string.Empty;
    public string TalkTitle { get; init; } = string.Empty;
    public int OrderIndex { get; init; }
    public bool IsRequired { get; init; }

    public string Status { get; init; } = string.Empty;
    public DateTime? CompletedAt { get; init; }

    public bool IsLocked { get; init; }
    public string? LockedReason { get; init; }
}
