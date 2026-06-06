using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;

public record WorkflowEventDto
{
    public string EventType { get; init; } = string.Empty;
    public TriggeredByType TriggeredByType { get; init; }
    public Guid? TriggeredByUserId { get; init; }
    public string? PayloadJson { get; init; }
    public DateTime OccurredAt { get; init; }
}
