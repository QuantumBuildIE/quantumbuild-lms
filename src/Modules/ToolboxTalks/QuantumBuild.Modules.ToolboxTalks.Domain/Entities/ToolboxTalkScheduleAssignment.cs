using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a specific employee assignment within a toolbox talk schedule.
/// Used when not assigning to all employees.
/// </summary>
public class ToolboxTalkScheduleAssignment : BaseEntity
{
    /// <summary>
    /// The schedule this assignment belongs to
    /// </summary>
    public Guid ScheduleId { get; set; }

    /// <summary>
    /// The employee to be assigned the toolbox talk
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Whether this assignment has been processed into a ScheduledTalk
    /// </summary>
    public bool IsProcessed { get; set; } = false;

    /// <summary>
    /// When the assignment was processed
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent schedule
    /// </summary>
    public ToolboxTalkSchedule Schedule { get; set; } = null!;

    /// <summary>
    /// The assigned employee
    /// </summary>
    public Employee Employee { get; set; } = null!;
}
