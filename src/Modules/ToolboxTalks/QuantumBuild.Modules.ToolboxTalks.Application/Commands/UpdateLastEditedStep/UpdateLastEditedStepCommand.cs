using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateLastEditedStep;

/// <summary>
/// Updates the LastEditedStep field on a ToolboxTalk to record wizard resume position.
/// Lightweight single-field update; does not touch sections, questions, or other content.
/// </summary>
public record UpdateLastEditedStepCommand : IRequest
{
    public Guid TenantId { get; init; }
    public Guid TalkId { get; init; }
    public int Step { get; init; }
}
