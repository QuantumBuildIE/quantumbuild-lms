namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for updating course assignment progress when scheduled talks are completed.
/// </summary>
public interface ICourseProgressService
{
    /// <summary>
    /// Updates the course assignment status based on completed scheduled talks.
    /// Transitions: Assigned → InProgress (first talk completed), InProgress → Completed (all required talks done).
    /// </summary>
    Task UpdateProgressAsync(Guid courseAssignmentId, CancellationToken cancellationToken = default);
}
