using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for auto-scheduling refresher training when a talk or course is completed.
/// </summary>
public interface IRefresherSchedulingService
{
    /// <summary>
    /// Checks if the completed standalone talk requires a refresher and schedules one if needed.
    /// </summary>
    Task ScheduleRefresherIfRequired(ScheduledTalk completedTalk, CancellationToken ct = default);

    /// <summary>
    /// Checks if the completed course assignment requires a refresher and schedules one if needed.
    /// </summary>
    Task ScheduleRefresherIfRequired(ToolboxTalkCourseAssignment completedAssignment, CancellationToken ct = default);
}
