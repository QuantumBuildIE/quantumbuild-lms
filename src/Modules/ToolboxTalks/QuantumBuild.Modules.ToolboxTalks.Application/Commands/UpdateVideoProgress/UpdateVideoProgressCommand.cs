using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateVideoProgress;

/// <summary>
/// Command to update video watch progress for a scheduled toolbox talk.
/// Called periodically as the employee watches the video.
/// </summary>
public record UpdateVideoProgressCommand : IRequest<VideoProgressDto>
{
    /// <summary>
    /// The scheduled talk being watched
    /// </summary>
    public Guid ScheduledTalkId { get; init; }

    /// <summary>
    /// Current watch progress percentage (0-100)
    /// </summary>
    public int WatchPercent { get; init; }
}

/// <summary>
/// Result of updating video progress
/// </summary>
public record VideoProgressDto
{
    /// <summary>
    /// Current watch progress percentage
    /// </summary>
    public int WatchPercent { get; init; }

    /// <summary>
    /// Minimum required watch percentage to complete the talk
    /// </summary>
    public int MinimumWatchPercent { get; init; }

    /// <summary>
    /// Whether the minimum watch requirement has been met
    /// </summary>
    public bool RequirementMet { get; init; }
}
