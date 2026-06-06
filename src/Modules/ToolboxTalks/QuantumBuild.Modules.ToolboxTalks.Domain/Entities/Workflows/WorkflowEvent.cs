using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;

public class WorkflowEvent : TenantEntity
{
    public WorkflowType WorkflowType { get; set; }
    public Guid TargetEntityId { get; set; }
    public string? TargetEntitySubKey { get; set; }
    public string EventType { get; set; } = string.Empty;
    public TriggeredByType TriggeredByType { get; set; }
    public Guid? TriggeredByUserId { get; set; }
    public string? PayloadJson { get; set; }
    public DateTime OccurredAt { get; set; }
}
