namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;

public record ToolboxTalkCourseAssignmentDto
{
    public Guid Id { get; init; }
    public Guid CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public string? CourseDescription { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeCode { get; init; }

    public DateTime AssignedAt { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }

    public string Status { get; init; } = string.Empty;
    public bool IsRefresher { get; init; }
    public DateTime? RefresherDueDate { get; init; }

    public int TotalTalks { get; init; }
    public int CompletedTalks { get; init; }
    public int ProgressPercent => TotalTalks > 0 ? (CompletedTalks * 100) / TotalTalks : 0;

    public List<CourseScheduledTalkDto> ScheduledTalks { get; init; } = new();
}
