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
}
