using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// One standard within a RegulatoryStructureMapPrinciple (e.g. "2.3"), holding every
/// person-experience and provider feature the document defines for it. StandardId is the
/// document's own printed identifier, stored literally — never normalised, never inferred.
/// System-managed (no TenantId) — inherits from BaseEntity.
/// </summary>
public class RegulatoryStructureMapStandard : BaseEntity
{
    public Guid RegulatoryStructureMapPrincipleId { get; set; }

    public string StandardId { get; set; } = string.Empty;

    /// <summary>Ordering among a principle's standards, document order.</summary>
    public int DisplayOrder { get; set; }

    // Navigation properties
    public RegulatoryStructureMapPrinciple RegulatoryStructureMapPrinciple { get; set; } = null!;
    public ICollection<RegulatoryStructureMapFeature> Features { get; set; } = new List<RegulatoryStructureMapFeature>();
}
