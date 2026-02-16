namespace QuantumBuild.Tests.Integration.Setup.Fakes;

/// <summary>
/// Fake email sender for capturing sent emails in integration tests.
/// </summary>
public class FakeEmailSender
{
    private readonly List<SentEmail> _sentEmails = new();

    /// <summary>
    /// Gets the list of emails that have been sent.
    /// </summary>
    public IReadOnlyList<SentEmail> SentEmails => _sentEmails.AsReadOnly();

    /// <summary>
    /// Sends an email (captures it for later verification).
    /// </summary>
    public Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _sentEmails.Add(new SentEmail(to, subject, body, DateTime.UtcNow));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends an email to multiple recipients.
    /// </summary>
    public Task SendEmailAsync(IEnumerable<string> to, string subject, string body, CancellationToken cancellationToken = default)
    {
        foreach (var recipient in to)
        {
            _sentEmails.Add(new SentEmail(recipient, subject, body, DateTime.UtcNow));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears all captured emails.
    /// </summary>
    public void Clear()
    {
        _sentEmails.Clear();
    }

    /// <summary>
    /// Gets the count of sent emails.
    /// </summary>
    public int Count => _sentEmails.Count;

    /// <summary>
    /// Gets the last sent email, if any.
    /// </summary>
    public SentEmail? LastEmail => _sentEmails.LastOrDefault();

    /// <summary>
    /// Gets emails sent to a specific recipient.
    /// </summary>
    public IEnumerable<SentEmail> GetEmailsTo(string email) =>
        _sentEmails.Where(e => e.To.Equals(email, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets emails with a specific subject (partial match).
    /// </summary>
    public IEnumerable<SentEmail> GetEmailsWithSubject(string subjectContains) =>
        _sentEmails.Where(e => e.Subject.Contains(subjectContains, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Represents an email that was captured by the fake email sender.
/// </summary>
public record SentEmail(string To, string Subject, string Body, DateTime SentAt);
