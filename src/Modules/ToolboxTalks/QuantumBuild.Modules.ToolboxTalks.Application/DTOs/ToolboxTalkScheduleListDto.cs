using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// Lightweight DTO for toolbox talk schedule lists
/// </summary>
public record ToolboxTalkScheduleListDto
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

    // Counts
    public int AssignmentCount { get; init; }
    public int ProcessedCount { get; init; }

    // Audit
    public DateTime CreatedAt { get; init; }
}
