using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.ProcessToolboxTalkSchedule;

/// <summary>
/// Command to process a toolbox talk schedule.
/// Creates ScheduledTalk records for unprocessed assignments.
/// Called by both manual action and background job.
/// </summary>
public record ProcessToolboxTalkScheduleCommand : IRequest<ProcessToolboxTalkScheduleResult>
{
    /// <summary>
    /// Tenant identifier
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Schedule identifier to process
    /// </summary>
    public Guid ScheduleId { get; init; }

    /// <summary>
    /// True when this run is dispatched by the recurring cron job, false for an on-demand
    /// "Process Now" call. Only a scheduled run advances NextRunDate/cadence for recurring
    /// schedules — an on-demand run processes whatever is currently unprocessed (and refreshes
    /// criteria-derived membership) without disturbing the schedule's cadence.
    /// </summary>
    public bool IsScheduledRun { get; init; } = false;
}

/// <summary>
/// Result of processing a toolbox talk schedule
/// </summary>
public record ProcessToolboxTalkScheduleResult
{
    /// <summary>
    /// Number of scheduled talks created
    /// </summary>
    public int TalksCreated { get; init; }

    /// <summary>
    /// Whether the schedule has been completed (one-time or end date reached)
    /// </summary>
    public bool ScheduleCompleted { get; init; }

    /// <summary>
    /// Next run date if schedule is recurring
    /// </summary>
    public DateTime? NextRunDate { get; init; }
}
