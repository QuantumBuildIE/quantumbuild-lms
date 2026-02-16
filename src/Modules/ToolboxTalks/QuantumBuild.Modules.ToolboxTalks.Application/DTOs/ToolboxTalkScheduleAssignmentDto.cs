namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for a schedule assignment to a specific employee
/// </summary>
public record ToolboxTalkScheduleAssignmentDto
{
    public Guid Id { get; init; }
    public Guid ScheduleId { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeEmail { get; init; }
    public bool IsProcessed { get; init; }
    public DateTime? ProcessedAt { get; init; }
}
