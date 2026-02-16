namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;

public record CourseAssignmentListDto
{
    public Guid Id { get; init; }
    public Guid CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public int TotalTalks { get; init; }
    public int CompletedTalks { get; init; }
    public DateTime AssignedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
