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
    /// <summary>
    /// Reviewer's edited content. Semantics depend on ReviewerType:
    /// - Internal: a single section's edited text (paired with the
    ///   section that the WorkflowReview targets).
    /// - External: a JSON array of [{ sectionIndex, translatedText }]
    ///   representing the whole session's edits. Auto-applied into
    ///   TranslatedSections by SubmitExternalReview when accepted.
    /// </summary>
    public string? EditedContent { get; set; }
    public string? DeclineReason { get; set; }
    public bool Accepted { get; set; }
    public DateTime SubmittedAt { get; set; }

    public ExternalParticipantInvitation? ExternalParticipantInvitation { get; set; }
}
