using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.DeleteToolboxTalk;

/// <summary>
/// Command to soft-delete a toolbox talk
/// </summary>
public record DeleteToolboxTalkCommand : IRequest<bool>
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
}
