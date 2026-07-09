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

    /// <summary>
    /// JSON-serialized <c>List&lt;int&gt;</c> of section indices this invitation may edit.
    /// Null means "no restriction, all sections editable" — the default that preserves
    /// full-scope review behaviour on every existing/legacy invitation row.
    /// </summary>
    public string? EditableSectionIndicesJson { get; set; }

    public Guid RequesterUserId { get; set; }
    public DateTime InvitedAt { get; set; }
    public DateTime? UsedAt { get; set; }
}
