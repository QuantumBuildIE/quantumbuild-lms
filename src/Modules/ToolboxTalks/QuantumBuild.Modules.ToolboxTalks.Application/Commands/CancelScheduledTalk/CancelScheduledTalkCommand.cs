using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CancelScheduledTalk;

/// <summary>
/// Command to cancel an individual scheduled talk assignment.
/// Sets the scheduled talk status to Cancelled.
/// </summary>
public record CancelScheduledTalkCommand : IRequest<bool>
{
    /// <summary>
    /// Tenant identifier
    /// </summary>
    public Guid TenantId { get; init; }

    /// <summary>
    /// Scheduled talk identifier to cancel
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Optional reason for cancellation
    /// </summary>
    public string? Reason { get; init; }
}
