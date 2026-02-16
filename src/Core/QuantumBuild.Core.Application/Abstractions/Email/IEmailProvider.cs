namespace QuantumBuild.Core.Application.Abstractions.Email;

/// <summary>
/// Provider-agnostic interface for sending emails.
/// Implementations can use SendGrid, SMTP, AWS SES, or any other email service.
/// </summary>
public interface IEmailProvider
{
    /// <summary>
    /// Indicates whether the provider is configured and ready to send emails.
    /// When false, emails will be logged but not actually sent.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Sends an email message
    /// </summary>
    /// <param name="message">The email message to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if email was sent successfully, false otherwise</returns>
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an email send operation
/// </summary>
public class EmailSendResult
{
    /// <summary>
    /// Whether the email was sent successfully
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if sending failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Provider-specific message ID if available
    /// </summary>
    public string? MessageId { get; init; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static EmailSendResult Succeeded(string? messageId = null) => new()
    {
        Success = true,
        MessageId = messageId
    };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static EmailSendResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };

    /// <summary>
    /// Creates a result indicating the provider is not configured
    /// </summary>
    public static EmailSendResult NotConfigured() => new()
    {
        Success = false,
        ErrorMessage = "Email provider is not configured"
    };
}
