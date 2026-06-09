namespace QuantumBuild.Core.Application.Interfaces;

/// <summary>
/// Service for sending email notifications
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a password setup email to a newly created user.
    /// When qrPin is provided a formatted PIN section is appended to the email.
    /// </summary>
    Task SendPasswordSetupEmailAsync(
        string email,
        string firstName,
        string resetToken,
        string? qrPin = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a generic email
    /// </summary>
    Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a standalone PIN notification email (no password setup link).
    /// Used for PIN resets and batch introduction emails.
    /// </summary>
    Task SendPinEmailAsync(
        string email,
        string firstName,
        string qrPin,
        string subject,
        string introText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a welcome notification to a newly created user account.
    /// Informs the user that their account exists and how to log in.
    /// Does not include password — admin provides it separately.
    /// </summary>
    Task SendUserCreatedEmailAsync(
        string email,
        string firstName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an external review invitation email to a third-party reviewer.
    /// </summary>
    Task SendExternalReviewInvitationEmailAsync(
        string reviewerEmail,
        string talkTitle,
        string languageName,
        DateTime expiresAt,
        string portalUrl,
        string requesterName,
        CancellationToken cancellationToken = default);
}
