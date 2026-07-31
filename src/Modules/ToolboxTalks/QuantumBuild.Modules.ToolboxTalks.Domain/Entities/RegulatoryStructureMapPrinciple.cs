using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// One principle (e.g. Principle 1) within a RegulatoryStructureMap, holding every standard the
/// document defines under it. System-managed (no TenantId) — inherits from BaseEntity.
/// </summary>
public class RegulatoryStructureMapPrinciple : BaseEntity
{
    public Guid RegulatoryStructureMapId { get; set; }

    public int Number { get; set; }

    /// <summary>Ordering among a map's principles, document order — not always equal to Number.</summary>
    public int DisplayOrder { get; set; }

    // Navigation properties
    public RegulatoryStructureMap RegulatoryStructureMap { get; set; } = null!;
    public ICollection<RegulatoryStructureMapStandard> Standards { get; set; } = new List<RegulatoryStructureMapStandard>();
}
