namespace QuantumBuild.Core.Application.Interfaces;

/// <summary>
/// Service for sending email notifications
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends a password setup email to a newly created user
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="firstName">User's first name for personalization</param>
    /// <param name="resetToken">Password reset token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendPasswordSetupEmailAsync(
        string email,
        string firstName,
        string resetToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a generic email
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlBody">HTML email body</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
