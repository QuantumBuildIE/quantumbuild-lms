using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents the assignment of a course to an employee.
/// When created, auto-generates ScheduledTalk records for each talk in the course.
/// Tracks progress at both course and individual talk level.
/// </summary>
public class ToolboxTalkCourseAssignment : TenantEntity
{
    public Guid CourseId { get; set; }
    public Guid EmployeeId { get; set; }

    public DateTime AssignedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public CourseAssignmentStatus Status { get; set; } = CourseAssignmentStatus.Assigned;

    // Refresher tracking (Phase 4)
    public bool IsRefresher { get; set; } = false;
    public Guid? OriginalAssignmentId { get; set; }
    public DateTime? RefresherDueDate { get; set; }
    public bool ReminderSent2Weeks { get; set; } = false;
    public bool ReminderSent1Week { get; set; } = false;

    // Navigation properties
    public virtual ToolboxTalkCourse Course { get; set; } = null!;
    public virtual Employee Employee { get; set; } = null!;
    public virtual ToolboxTalkCourseAssignment? OriginalAssignment { get; set; }
    public virtual ICollection<ScheduledTalk> ScheduledTalks { get; set; } = new List<ScheduledTalk>();
}
