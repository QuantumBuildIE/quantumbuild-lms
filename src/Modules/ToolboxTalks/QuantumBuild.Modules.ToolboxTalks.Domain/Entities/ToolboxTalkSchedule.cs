using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a schedule for assigning a toolbox talk to employees.
/// Can be one-time or recurring based on frequency.
/// </summary>
public class ToolboxTalkSchedule : TenantEntity
{
    /// <summary>
    /// The toolbox talk to be assigned
    /// </summary>
    public Guid ToolboxTalkId { get; set; }

    /// <summary>
    /// Date when the talk should be assigned
    /// </summary>
    public DateTime ScheduledDate { get; set; }

    /// <summary>
    /// End date for recurring schedules (null for one-time or indefinite)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Frequency at which the schedule recurs
    /// </summary>
    public ToolboxTalkFrequency Frequency { get; set; } = ToolboxTalkFrequency.Once;

    /// <summary>
    /// If true, assigns to all active employees regardless of specific assignments
    /// </summary>
    public bool AssignToAllEmployees { get; set; } = false;

    /// <summary>
    /// Current status of the schedule
    /// </summary>
    public ToolboxTalkScheduleStatus Status { get; set; } = ToolboxTalkScheduleStatus.Draft;

    /// <summary>
    /// Next date when the schedule will run (for recurring schedules)
    /// </summary>
    public DateTime? NextRunDate { get; set; }

    /// <summary>
    /// Additional notes about the schedule
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties

    /// <summary>
    /// The toolbox talk being scheduled
    /// </summary>
    public ToolboxTalk ToolboxTalk { get; set; } = null!;

    /// <summary>
    /// Specific employee assignments for this schedule (when not assigning to all)
    /// </summary>
    public ICollection<ToolboxTalkScheduleAssignment> Assignments { get; set; } = new List<ToolboxTalkScheduleAssignment>();
}
