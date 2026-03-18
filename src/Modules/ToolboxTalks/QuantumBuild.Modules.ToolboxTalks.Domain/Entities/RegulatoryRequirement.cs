using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// A specific compliance obligation within a regulatory profile.
/// System-managed (no TenantId) — inherits from BaseEntity.
/// IngestionStatus gates visibility: only Approved requirements are shown to tenants.
/// </summary>
public class RegulatoryRequirement : BaseEntity
{
    public Guid RegulatoryProfileId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Section { get; set; }

    public string? SectionLabel { get; set; }

    public string? Principle { get; set; }

    public string? PrincipleLabel { get; set; }

    public string Priority { get; set; } = "med";

    public int DisplayOrder { get; set; }

    public RequirementIngestionSource IngestionSource { get; set; } = RequirementIngestionSource.Manual;

    public RequirementIngestionStatus IngestionStatus { get; set; } = RequirementIngestionStatus.Draft;

    /// <summary>
    /// Reviewer notes on ingestion decision
    /// </summary>
    public string? IngestionNotes { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public RegulatoryProfile RegulatoryProfile { get; set; } = null!;
    public ICollection<RegulatoryRequirementMapping> Mappings { get; set; } = new List<RegulatoryRequirementMapping>();
}
