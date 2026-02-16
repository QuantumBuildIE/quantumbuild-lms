using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.StartToolboxTalk;

/// <summary>
/// Command to record that an employee has started viewing a toolbox talk.
/// Transitions status to InProgress and captures optional geolocation.
/// </summary>
public record StartToolboxTalkCommand : IRequest<Unit>
{
    /// <summary>
    /// The scheduled talk being started
    /// </summary>
    public Guid ScheduledTalkId { get; init; }

    /// <summary>
    /// Latitude of the employee when starting the talk
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Longitude of the employee when starting the talk
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// GPS accuracy in meters
    /// </summary>
    public double? AccuracyMeters { get; init; }
}
