using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for a toolbox talk schedule
/// </summary>
public record ToolboxTalkScheduleDto
{
    public Guid Id { get; init; }
    public Guid ToolboxTalkId { get; init; }
    public string ToolboxTalkTitle { get; init; } = string.Empty;
    public DateTime ScheduledDate { get; init; }
    public DateTime? EndDate { get; init; }
    public ToolboxTalkFrequency Frequency { get; init; }
    public string FrequencyDisplay { get; init; } = string.Empty;
    public bool AssignToAllEmployees { get; init; }
    public ToolboxTalkScheduleStatus Status { get; init; }
    public string StatusDisplay { get; init; } = string.Empty;
    public DateTime? NextRunDate { get; init; }
    public string? Notes { get; init; }

    // Assignment counts
    public int AssignmentCount { get; init; }
    public int ProcessedCount { get; init; }

    // Specific assignments (when not assigning to all)
    public List<ToolboxTalkScheduleAssignmentDto> Assignments { get; init; } = new();

    // Audit
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
