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

    // Notification toggles — each independently controls one email notification type.
    // All default to true so notifications work out-of-the-box.
    // Recipients: all Admin users on the tenant.

    public bool NotifyOnTranslationComplete { get; set; } = true;
    public bool NotifyOnValidationComplete { get; set; } = true;
    public bool NotifyOnFailure { get; set; } = true;
    public bool NotifyOnExternalReviewResponse { get; set; } = true;

    // Learning-wizard toggle defaults — inherited by new talks at creation time (creation-time
    // snapshot: changing a tenant default does not retroactively affect existing talks).
    // Consumed by InitialiseToolboxTalkCommandHandler, same pattern as DefaultIsActive above.

    /// <summary>
    /// Whether the video-rights confirmation checkbox is pre-checked by default in the wizard.
    /// UI-only gate — never persisted to ToolboxTalk (videoRightsConfirmed is not sent to the
    /// backend), so this setting has no InitialiseToolboxTalkCommandHandler inheritance step.
    /// </summary>
    public bool DefaultVideoRightsConfirmed { get; set; } = false;

    /// <summary>
    /// Whether new talks randomly select quiz questions from a larger pool by default.
    /// </summary>
    public bool DefaultUseQuestionPool { get; set; } = false;

    /// <summary>
    /// Whether new PDF-sourced talks generate a slide-image slideshow by default.
    /// </summary>
    public bool DefaultGenerateSlideshow { get; set; } = false;

    /// <summary>
    /// Whether new talks are auto-assigned to new employees by default.
    /// </summary>
    public bool DefaultAutoAssign { get; set; } = true;

    /// <summary>
    /// Whether AI generation preserves source wording as closely as possible by default.
    /// </summary>
    public bool DefaultPreserveSourceWording { get; set; } = true;

    /// <summary>
    /// Whether quiz question order is shuffled per attempt by default.
    /// </summary>
    public bool DefaultShuffleQuestions { get; set; } = true;

    /// <summary>
    /// Whether answer option order is shuffled per question by default.
    /// </summary>
    public bool DefaultShuffleOptions { get; set; } = true;

    /// <summary>
    /// Whether new talks require a quiz by default.
    /// </summary>
    public bool DefaultIncludeQuiz { get; set; } = true;

    /// <summary>
    /// Whether employees may retake a failed quiz without rewatching the video, by default.
    /// </summary>
    public bool DefaultAllowRetry { get; set; } = true;
}
