using System.ComponentModel.DataAnnotations;
using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Tenant-level configuration settings for the Toolbox Talks module.
/// One record per tenant.
/// </summary>
public class ToolboxTalkSettings : BaseEntity
{
    /// <summary>
    /// Tenant identifier (unique - one settings record per tenant)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Default number of days employees have to complete a talk after assignment
    /// </summary>
    public int DefaultDueDays { get; set; } = 7;

    /// <summary>
    /// How often to send reminders (in days)
    /// </summary>
    public int ReminderFrequencyDays { get; set; } = 1;

    /// <summary>
    /// Maximum number of reminder notifications to send
    /// </summary>
    public int MaxReminders { get; set; } = 5;

    /// <summary>
    /// Number of reminders before escalating to management
    /// </summary>
    public int EscalateAfterReminders { get; set; } = 3;

    /// <summary>
    /// Whether videos must be fully watched before completion
    /// </summary>
    public bool RequireVideoCompletion { get; set; } = true;

    /// <summary>
    /// Default passing score percentage for quizzes (0-100)
    /// </summary>
    public int DefaultPassingScore { get; set; } = 80;

    /// <summary>
    /// Whether AI translation is enabled for content
    /// </summary>
    public bool EnableTranslation { get; set; } = false;

    /// <summary>
    /// Translation provider to use (e.g., "Claude", "OpenAI")
    /// </summary>
    [MaxLength(50)]
    public string? TranslationProvider { get; set; }

    /// <summary>
    /// Whether AI video dubbing is enabled
    /// </summary>
    public bool EnableVideoDubbing { get; set; } = false;

    /// <summary>
    /// Video dubbing provider to use (e.g., "ElevenLabs")
    /// </summary>
    [MaxLength(50)]
    public string? VideoDubbingProvider { get; set; }

    /// <summary>
    /// Email template for initial notification (supports placeholders)
    /// </summary>
    public string? NotificationEmailTemplate { get; set; }

    /// <summary>
    /// Email template for reminder notifications (supports placeholders)
    /// </summary>
    public string? ReminderEmailTemplate { get; set; }

    // Wizard Step 4 defaults — applied to new talks at creation time by InitialiseToolboxTalkCommandHandler

    /// <summary>
    /// Default minimum percentage of video an employee must watch before the talk can be completed (50–100).
    /// Consumed by wizard Step 4 via InitialiseToolboxTalkCommandHandler at talk creation.
    /// </summary>
    public int DefaultMinimumVideoWatchPercent { get; set; } = 90;

    /// <summary>
    /// Default number of days after hire date before an auto-assigned talk is due (1–90).
    /// Consumed by wizard Step 4 via InitialiseToolboxTalkCommandHandler at talk creation.
    /// </summary>
    public int DefaultAutoAssignDueDays { get; set; } = 14;

    /// <summary>
    /// Whether new talks should generate a PDF certificate on completion by default.
    /// Consumed by wizard Step 4 via InitialiseToolboxTalkCommandHandler at talk creation.
    /// </summary>
    public bool DefaultGenerateCertificate { get; set; } = true;

    /// <summary>
    /// Default refresher schedule for new talks: Once, Monthly, Quarterly, or Annually.
    /// Consumed by wizard Step 4 via InitialiseToolboxTalkCommandHandler at talk creation.
    /// </summary>
    [MaxLength(20)]
    public string DefaultRefresherFrequency { get; set; } = "Once";

    /// <summary>
    /// Whether new talks should be active (IsActive = true) by default at creation.
    /// IsActive is not a learner-visibility gate — assignment records control visibility.
    /// Consumed by wizard Step 4 via InitialiseToolboxTalkCommandHandler at talk creation.
    /// </summary>
    public bool DefaultIsActive { get; set; } = true;
}
