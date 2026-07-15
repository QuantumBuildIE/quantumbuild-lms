using MediatR;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.ToggleToolboxTalkActive;

/// <summary>
/// Sets the IsActive flag on a ToolboxTalk. Lightweight single-field update; does not touch
/// sections, questions, or trigger translation-stalening side effects.
/// </summary>
public record ToggleToolboxTalkActiveCommand : IRequest<bool>
{
    public Guid TenantId { get; init; }
    public Guid TalkId { get; init; }
    public bool Active { get; init; }
}
