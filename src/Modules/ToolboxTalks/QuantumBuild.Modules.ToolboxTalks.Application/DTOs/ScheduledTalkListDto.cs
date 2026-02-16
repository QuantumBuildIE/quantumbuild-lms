using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// Lightweight DTO for scheduled talk lists (admin view)
/// </summary>
public record ScheduledTalkListDto
{
    public Guid Id { get; init; }
    public Guid ToolboxTalkId { get; init; }
    public string ToolboxTalkTitle { get; init; } = string.Empty;
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = string.Empty;
    public string? EmployeeEmail { get; init; }
    public Guid? ScheduleId { get; init; }
    public DateTime RequiredDate { get; init; }
    public DateTime DueDate { get; init; }
    public ScheduledTalkStatus Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public int RemindersSent { get; init; }

    // Progress
    public int TotalSections { get; init; }
    public int CompletedSections { get; init; }
    public decimal ProgressPercent { get; init; }

    // Audit
    public DateTime CreatedAt { get; init; }
}
