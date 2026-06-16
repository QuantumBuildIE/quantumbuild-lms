namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for tenant-level toolbox talk settings
/// </summary>
public record ToolboxTalkSettingsDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }

    // Due dates and reminders
    public int DefaultDueDays { get; init; }
    public int ReminderFrequencyDays { get; init; }
    public int MaxReminders { get; init; }
    public int EscalateAfterReminders { get; init; }

    // Video settings
    public bool RequireVideoCompletion { get; init; }

    // Quiz settings
    public int DefaultPassingScore { get; init; }

    // Translation settings
    public bool EnableTranslation { get; init; }
    public string? TranslationProvider { get; init; }

    // Video dubbing settings
    public bool EnableVideoDubbing { get; init; }
    public string? VideoDubbingProvider { get; init; }

    // Email templates
    public string? NotificationEmailTemplate { get; init; }
    public string? ReminderEmailTemplate { get; init; }

    // Wizard Step 4 defaults
    public int DefaultMinimumVideoWatchPercent { get; init; }
    public int DefaultAutoAssignDueDays { get; init; }
    public bool DefaultGenerateCertificate { get; init; }
    public string DefaultRefresherFrequency { get; init; } = "Once";
    public bool DefaultIsActive { get; init; }
}
