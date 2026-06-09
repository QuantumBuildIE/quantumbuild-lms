using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;

public class ExternalParticipantInvitation : TenantEntity
{
    public WorkflowType WorkflowType { get; set; }
    public Guid TargetEntityId { get; set; }
    public string? TargetEntitySubKey { get; set; }
    public string InvitedEmail { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public InvitationStatus Status { get; set; }
    public string ContextType { get; set; } = string.Empty;
    public string? ContextPayload { get; set; }
    public Guid RequesterUserId { get; set; }
    public DateTime InvitedAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
