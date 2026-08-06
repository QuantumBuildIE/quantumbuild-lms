using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Tests.Integration.Setup.Fakes;

/// <summary>
/// Fake IToolboxTalkEmailService that captures calls for assertion in integration tests
/// and can be configured to throw so tests can verify failure handling (CertificateEmailFailed,
/// completion still succeeding, etc.) without depending on a real email provider.
/// </summary>
public class FakeToolboxTalkEmailService : IToolboxTalkEmailService
{
    private readonly List<SentCompletionEmail> _completionEmails = new();
    private readonly List<SentCourseCompletionEmail> _courseCompletionEmails = new();

    /// <summary>When true, SendCompletionConfirmationEmailAsync throws to simulate delivery failure.</summary>
    public bool ShouldThrowOnCompletionEmail { get; set; }

    /// <summary>When true, SendCourseCompletionConfirmationEmailAsync throws to simulate delivery failure.</summary>
    public bool ShouldThrowOnCourseCompletionEmail { get; set; }

    public IReadOnlyList<SentCompletionEmail> CompletionEmails => _completionEmails.AsReadOnly();
    public IReadOnlyList<SentCourseCompletionEmail> CourseCompletionEmails => _courseCompletionEmails.AsReadOnly();

    public void Reset()
    {
        _completionEmails.Clear();
        _courseCompletionEmails.Clear();
        ShouldThrowOnCompletionEmail = false;
        ShouldThrowOnCourseCompletionEmail = false;
    }

    public Task SendTalkAssignmentEmailAsync(ScheduledTalk scheduledTalk, Employee employee, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendReminderEmailAsync(ScheduledTalk scheduledTalk, Employee employee, int reminderNumber, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendCompletionConfirmationEmailAsync(ScheduledTalkCompletion completion, Employee employee, CancellationToken cancellationToken = default)
    {
        if (ShouldThrowOnCompletionEmail)
            throw new InvalidOperationException("Simulated completion email delivery failure.");

        _completionEmails.Add(new SentCompletionEmail(completion.Id, completion.ScheduledTalkId, employee.Id, completion.CertificateUrl));
        return Task.CompletedTask;
    }

    public Task SendCourseCompletionConfirmationEmailAsync(ToolboxTalkCourseAssignment courseAssignment, Employee employee, string? certificateUrl, CancellationToken cancellationToken = default)
    {
        if (ShouldThrowOnCourseCompletionEmail)
            throw new InvalidOperationException("Simulated course completion email delivery failure.");

        _courseCompletionEmails.Add(new SentCourseCompletionEmail(courseAssignment.Id, employee.Id, certificateUrl));
        return Task.CompletedTask;
    }

    public Task SendEscalationEmailAsync(ScheduledTalk scheduledTalk, Employee employee, Employee manager, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendRefresherReminderAsync(ScheduledTalk refresherTalk, Employee employee, string timeframe, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendCourseAssignmentEmailAsync(ToolboxTalkCourse course, Employee employee, int talkCount, DateTime? dueDate, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SendCourseRefresherReminderAsync(ToolboxTalkCourseAssignment refresherAssignment, Employee employee, string timeframe, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public record SentCompletionEmail(Guid CompletionId, Guid ScheduledTalkId, Guid EmployeeId, string? CertificateUrl);

public record SentCourseCompletionEmail(Guid CourseAssignmentId, Guid EmployeeId, string? CertificateUrl);
