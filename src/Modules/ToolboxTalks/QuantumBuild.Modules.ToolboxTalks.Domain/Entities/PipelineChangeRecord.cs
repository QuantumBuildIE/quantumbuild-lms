using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Append-only audit record of a single pipeline change that produced a new PipelineVersion.
/// System-level — no TenantId.
/// Do not add update or delete endpoints for this entity.
/// </summary>
public class PipelineChangeRecord : BaseEntity
{
    /// <summary>Auto-generated sequential identifier, e.g. "CR-001".</summary>
    public string ChangeId { get; set; } = string.Empty;

    /// <summary>Pipeline component that changed, e.g. "Round 3 back-translator".</summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>Previous value, e.g. "DeepSeek v3.1".</summary>
    public string ChangeFrom { get; set; } = string.Empty;

    /// <summary>New value, e.g. "claude-sonnet-4-20250514".</summary>
    public string ChangeTo { get; set; } = string.Empty;

    /// <summary>Reason for the change.</summary>
    public string Justification { get; set; } = string.Empty;

    /// <summary>Optional assessment of the change's impact on validation quality.</summary>
    public string? ImpactAssessment { get; set; }

    /// <summary>Optional action taken on prior validation runs affected by this change.</summary>
    public string? PriorModulesAction { get; set; }

    /// <summary>Name or identifier of the person who approved this change.</summary>
    public string? Approver { get; set; }

    /// <summary>UTC timestamp when this change was deployed.</summary>
    public DateTimeOffset DeployedAt { get; set; }

    /// <summary>Workflow status of this change record.</summary>
    public PipelineChangeStatus Status { get; set; } = PipelineChangeStatus.Draft;

    // FK to the new pipeline version this change produced
    public Guid PipelineVersionId { get; set; }
    public PipelineVersion PipelineVersion { get; set; } = null!;

    // FK to the previous pipeline version (null for the very first record)
    public Guid? PreviousPipelineVersionId { get; set; }
}
