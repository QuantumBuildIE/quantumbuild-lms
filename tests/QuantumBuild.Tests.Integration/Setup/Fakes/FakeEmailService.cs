using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.Tests.Integration.Setup.Fakes;

/// <summary>
/// Fake IEmailService that captures calls for assertion in integration tests.
/// Register as singleton in CustomWebApplicationFactory so tests can inspect it.
/// </summary>
public class FakeEmailService : IEmailService
{
    private readonly List<SentExternalReviewInvitation> _invitations = new();

    /// <summary>
    /// When true, SendExternalReviewInvitationEmailAsync throws to simulate delivery failure.
    /// Reset to false between tests via Reset().
    /// </summary>
    public bool ShouldThrowOnInvitationEmail { get; set; }

    public IReadOnlyList<SentExternalReviewInvitation> SentInvitations => _invitations.AsReadOnly();

    public void Reset()
    {
        _invitations.Clear();
        ShouldThrowOnInvitationEmail = false;
    }

    public Task SendPasswordSetupEmailAsync(string email, string firstName, string resetToken,
        string? qrPin = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendEmailAsync(string to, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendPinEmailAsync(string email, string firstName, string qrPin,
        string subject, string introText, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendUserCreatedEmailAsync(string email, string firstName,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendExternalReviewInvitationEmailAsync(
        string reviewerEmail,
        string talkTitle,
        string languageName,
        DateTime expiresAt,
        string portalUrl,
        string requesterName,
        CancellationToken cancellationToken = default)
    {
        if (ShouldThrowOnInvitationEmail)
            throw new InvalidOperationException("Simulated email delivery failure.");

        _invitations.Add(new SentExternalReviewInvitation(
            reviewerEmail, talkTitle, languageName, expiresAt, portalUrl, requesterName));

        return Task.CompletedTask;
    }
}

public record SentExternalReviewInvitation(
    string ReviewerEmail,
    string TalkTitle,
    string LanguageName,
    DateTime ExpiresAt,
    string PortalUrl,
    string RequesterName);
