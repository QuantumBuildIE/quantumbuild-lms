using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CompleteToolboxTalk;

/// <summary>
/// Command to complete a scheduled toolbox talk.
/// Requires signature and validates all completion requirements.
/// </summary>
public record CompleteToolboxTalkCommand : IRequest<ScheduledTalkCompletionDto>
{
    /// <summary>
    /// The scheduled talk to complete
    /// </summary>
    public Guid ScheduledTalkId { get; init; }

    /// <summary>
    /// Base64 encoded signature image (PNG)
    /// </summary>
    public string SignatureData { get; init; } = string.Empty;

    /// <summary>
    /// Name entered by the employee when signing
    /// </summary>
    public string SignedByName { get; init; } = string.Empty;

    /// <summary>
    /// Latitude of the employee when completing the talk
    /// </summary>
    public double? Latitude { get; init; }

    /// <summary>
    /// Longitude of the employee when completing the talk
    /// </summary>
    public double? Longitude { get; init; }

    /// <summary>
    /// GPS accuracy in meters
    /// </summary>
    public double? AccuracyMeters { get; init; }
}
