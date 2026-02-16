using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for sending Toolbox Talk related email notifications
/// </summary>
public interface IToolboxTalkEmailService
{
    /// <summary>
    /// Sends an email to an employee when a new toolbox talk is assigned to them
    /// </summary>
    Task SendTalkAssignmentEmailAsync(
        ScheduledTalk scheduledTalk,
        Employee employee,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a reminder email for an overdue toolbox talk
    /// </summary>
    Task SendReminderEmailAsync(
        ScheduledTalk scheduledTalk,
        Employee employee,
        int reminderNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a confirmation email when an employee completes a toolbox talk
    /// </summary>
    Task SendCompletionConfirmationEmailAsync(
        ScheduledTalkCompletion completion,
        Employee employee,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an escalation email to the employee's manager when a talk remains overdue
    /// </summary>
    Task SendEscalationEmailAsync(
        ScheduledTalk scheduledTalk,
        Employee employee,
        Employee manager,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a refresher reminder email for an upcoming standalone talk refresher
    /// </summary>
    Task SendRefresherReminderAsync(
        ScheduledTalk refresherTalk,
        Employee employee,
        string timeframe,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an email to an employee when a training course is assigned to them
    /// </summary>
    Task SendCourseAssignmentEmailAsync(
        ToolboxTalkCourse course,
        Employee employee,
        int talkCount,
        DateTime? dueDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a refresher reminder email for an upcoming course refresher
    /// </summary>
    Task SendCourseRefresherReminderAsync(
        ToolboxTalkCourseAssignment refresherAssignment,
        Employee employee,
        string timeframe,
        CancellationToken cancellationToken = default);
}
