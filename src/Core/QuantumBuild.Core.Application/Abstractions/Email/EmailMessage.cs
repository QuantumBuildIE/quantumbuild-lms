namespace QuantumBuild.Core.Application.Abstractions.Email;

/// <summary>
/// Represents an email message to be sent
/// </summary>
public class EmailMessage
{
    /// <summary>
    /// Recipient email address
    /// </summary>
    public required string ToEmail { get; set; }

    /// <summary>
    /// Recipient display name
    /// </summary>
    public string? ToName { get; set; }

    /// <summary>
    /// Email subject line
    /// </summary>
    public required string Subject { get; set; }

    /// <summary>
    /// HTML body content
    /// </summary>
    public required string HtmlBody { get; set; }

    /// <summary>
    /// Plain text body (optional, for clients that don't support HTML)
    /// </summary>
    public string? PlainTextBody { get; set; }

    /// <summary>
    /// Reply-to email address (optional, overrides From email for replies)
    /// </summary>
    public string? ReplyToEmail { get; set; }

    /// <summary>
    /// Reply-to display name
    /// </summary>
    public string? ReplyToName { get; set; }
}
