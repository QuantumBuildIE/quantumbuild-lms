using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents an individual toolbox talk assignment to a specific employee.
/// Created from schedules or manually assigned.
/// </summary>
public class ScheduledTalk : TenantEntity
{
    /// <summary>
    /// The toolbox talk to be completed
    /// </summary>
    public Guid ToolboxTalkId { get; set; }

    /// <summary>
    /// The employee assigned to complete this talk
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// The schedule that created this assignment (null if manually assigned)
    /// </summary>
    public Guid? ScheduleId { get; set; }

    /// <summary>
    /// The course assignment that created this scheduled talk (null if standalone assignment)
    /// </summary>
    public Guid? CourseAssignmentId { get; set; }

    /// <summary>
    /// Order index within the course (for display and sequential enforcement)
    /// </summary>
    public int? CourseOrderIndex { get; set; }

    /// <summary>
    /// Date when the talk was assigned/required
    /// </summary>
    public DateTime RequiredDate { get; set; }

    /// <summary>
    /// Deadline for completing the talk
    /// </summary>
    public DateTime DueDate { get; set; }

    /// <summary>
    /// Current status of this assignment
    /// </summary>
    public ScheduledTalkStatus Status { get; set; } = ScheduledTalkStatus.Pending;

    /// <summary>
    /// Number of reminder notifications sent
    /// </summary>
    public int RemindersSent { get; set; } = 0;

    /// <summary>
    /// When the last reminder was sent
    /// </summary>
    public DateTime? LastReminderAt { get; set; }

    /// <summary>
    /// Language code for the employee's preferred language (e.g., "en", "es", "pl")
    /// </summary>
    public string LanguageCode { get; set; } = "en";

    /// <summary>
    /// Current video watch progress percentage (0-100)
    /// Updated as employee watches the video
    /// </summary>
    public int VideoWatchPercent { get; set; } = 0;

    // Geolocation when started
    public double? StartedLatitude { get; set; }
    public double? StartedLongitude { get; set; }
    public double? StartedAccuracyMeters { get; set; }
    public DateTime? StartedLocationTimestamp { get; set; }

    // Refresher tracking (Phase 4)

    /// <summary>
    /// Whether this scheduled talk is a refresher of a previously completed talk
    /// </summary>
    public bool IsRefresher { get; set; } = false;

    /// <summary>
    /// Links to the original scheduled talk that this refresher is based on
    /// </summary>
    public Guid? OriginalScheduledTalkId { get; set; }

    /// <summary>
    /// When the refresher training is due
    /// </summary>
    public DateTime? RefresherDueDate { get; set; }

    /// <summary>
    /// Whether the 2-week reminder has been sent
    /// </summary>
    public bool ReminderSent2Weeks { get; set; } = false;

    /// <summary>
    /// Whether the 1-week reminder has been sent
    /// </summary>
    public bool ReminderSent1Week { get; set; } = false;

    // Navigation properties

    /// <summary>
    /// The toolbox talk to be completed
    /// </summary>
    public ToolboxTalk ToolboxTalk { get; set; } = null!;

    /// <summary>
    /// The assigned employee
    /// </summary>
    public Employee Employee { get; set; } = null!;

    /// <summary>
    /// The schedule that created this assignment (if any)
    /// </summary>
    public ToolboxTalkSchedule? Schedule { get; set; }

    /// <summary>
    /// The course assignment that created this scheduled talk (if any)
    /// </summary>
    public ToolboxTalkCourseAssignment? CourseAssignment { get; set; }

    /// <summary>
    /// The original scheduled talk that this refresher is based on (if any)
    /// </summary>
    public ScheduledTalk? OriginalScheduledTalk { get; set; }

    /// <summary>
    /// Progress tracking for each section
    /// </summary>
    public ICollection<ScheduledTalkSectionProgress> SectionProgress { get; set; } = new List<ScheduledTalkSectionProgress>();

    /// <summary>
    /// Quiz attempts for this assignment
    /// </summary>
    public ICollection<ScheduledTalkQuizAttempt> QuizAttempts { get; set; } = new List<ScheduledTalkQuizAttempt>();

    /// <summary>
    /// Completion record when talk is finished
    /// </summary>
    public ScheduledTalkCompletion? Completion { get; set; }
}
