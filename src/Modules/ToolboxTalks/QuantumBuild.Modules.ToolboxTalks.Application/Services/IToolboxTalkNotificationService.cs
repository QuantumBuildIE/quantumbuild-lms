namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Per-language result passed to translation completion notifications.
/// </summary>
public record TranslationLanguageResult(
    string LanguageName,
    string LanguageCode,
    bool Success,
    string? ErrorMessage);

/// <summary>
/// Sends admin notification emails for translation and validation pipeline events.
/// Each notification type is independently togglable via ToolboxTalkSettings.
/// Recipients are all active Admin users on the tenant.
/// All methods swallow exceptions — a notification failure must never fail the calling operation.
/// </summary>
public interface IToolboxTalkNotificationService
{
    /// <summary>
    /// Fires when AI content translation completes for a talk (full batch, success or partial failure).
    /// Guarded by <c>NotifyOnTranslationComplete</c>.
    /// </summary>
    Task NotifyTranslationCompleteAsync(
        Guid tenantId,
        Guid talkId,
        string talkTitle,
        IReadOnlyList<TranslationLanguageResult> results,
        CancellationToken ct = default);

    /// <summary>
    /// Fires when a translation validation run completes.
    /// Guarded by <c>NotifyOnValidationComplete</c>.
    /// </summary>
    Task NotifyValidationCompleteAsync(
        Guid tenantId,
        Guid talkId,
        string talkTitle,
        string languageName,
        string outcome,
        double? score,
        int passedSections,
        int totalSections,
        CancellationToken ct = default);

    /// <summary>
    /// Fires when a translation or validation job crashes with an unhandled exception.
    /// Guarded by <c>NotifyOnFailure</c>.
    /// </summary>
    Task NotifyFailureAsync(
        Guid tenantId,
        Guid talkId,
        string talkTitle,
        string failureContext,
        string errorMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Fires when an external reviewer submits their response (accepted or rejected).
    /// Guarded by <c>NotifyOnExternalReviewResponse</c>.
    /// </summary>
    Task NotifyExternalReviewResponseAsync(
        Guid tenantId,
        Guid talkId,
        string talkTitle,
        string languageName,
        bool accepted,
        CancellationToken ct = default);
}
