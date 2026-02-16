using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.SendScheduledTalkReminder;

/// <summary>
/// Command to manually send a reminder for a scheduled talk assignment.
/// Increments the reminder count and triggers a reminder email.
/// </summary>
public record SendScheduledTalkReminderCommand : IRequest<bool>
{
    /// <summary>
    /// Tenant identifier
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Scheduled talk identifier to send reminder for
    /// </summary>
    public Guid Id { get; init; }
}
