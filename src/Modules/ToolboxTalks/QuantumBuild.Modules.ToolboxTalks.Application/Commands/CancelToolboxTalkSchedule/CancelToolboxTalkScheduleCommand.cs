using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CancelToolboxTalkSchedule;

/// <summary>
/// Command to cancel a toolbox talk schedule.
/// Sets the schedule status to Cancelled and optionally cancels pending scheduled talks.
/// </summary>
public record CancelToolboxTalkScheduleCommand : IRequest<bool>
{
    /// <summary>
    /// Tenant identifier
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Schedule identifier to cancel
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// If true, also cancels any pending ScheduledTalks created from this schedule
    /// </summary>
    public bool CancelPendingTalks { get; init; } = true;
}
