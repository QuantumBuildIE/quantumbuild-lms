using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// First-class sector entity. The Key field is the canonical string that ties to
/// SafetyGlossary.SectorKey and TranslationValidationRun.SectorKey.
/// </summary>
public class Sector : BaseEntity
{
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<TenantSector> TenantSectors { get; set; } = new List<TenantSector>();
}
