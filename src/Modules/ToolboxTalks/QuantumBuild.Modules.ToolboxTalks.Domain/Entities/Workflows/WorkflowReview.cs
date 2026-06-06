using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;

public class WorkflowReview : TenantEntity
{
    public WorkflowType WorkflowType { get; set; }
    public Guid TargetEntityId { get; set; }
    public string? TargetEntitySubKey { get; set; }
    public ReviewerType ReviewerType { get; set; }
    public Guid? ReviewerUserId { get; set; }
    public Guid? ExternalParticipantInvitationId { get; set; }
    public string? EditedContent { get; set; }
    public bool Accepted { get; set; }
    public DateTime SubmittedAt { get; set; }

    public ExternalParticipantInvitation? ExternalParticipantInvitation { get; set; }
}
