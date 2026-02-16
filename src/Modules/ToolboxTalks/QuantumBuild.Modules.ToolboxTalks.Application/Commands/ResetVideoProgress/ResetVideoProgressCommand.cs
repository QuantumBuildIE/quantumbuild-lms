using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateVideoProgress;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.ResetVideoProgress;

/// <summary>
/// Command to reset video watch progress for a scheduled toolbox talk.
/// Used when an employee fails a quiz and chooses to rewatch the video.
/// </summary>
public record ResetVideoProgressCommand : IRequest<VideoProgressDto>
{
    /// <summary>
    /// The scheduled talk whose video progress should be reset
    /// </summary>
    public Guid ScheduledTalkId { get; init; }
}
