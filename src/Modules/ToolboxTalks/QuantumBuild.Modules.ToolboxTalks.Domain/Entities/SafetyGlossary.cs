using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Sector-based safety glossary definition.
/// TenantId = null means system-wide default; a specific TenantId means tenant override.
/// </summary>
public class SafetyGlossary : BaseEntity
{
    // Nullable TenantId: null = system default, Guid = tenant override
    public Guid? TenantId { get; set; }

    public string SectorKey { get; set; } = string.Empty;
    public string SectorName { get; set; } = string.Empty;
    public string? SectorIcon { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<SafetyGlossaryTerm> Terms { get; set; } = new List<SafetyGlossaryTerm>();
}
