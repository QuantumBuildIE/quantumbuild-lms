using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Individual criteria item within a regulatory profile.
/// Supports tenant overrides following the SafetyGlossary pattern:
/// TenantId = null means system default; a specific TenantId means tenant override.
/// </summary>
public class RegulatoryCriteria : BaseEntity
{
    public Guid RegulatoryProfileId { get; set; }

    /// <summary>
    /// Nullable TenantId: null = system default, Guid = tenant override
    /// </summary>
    public Guid? TenantId { get; set; }

    public string CategoryKey { get; set; } = string.Empty;

    public string CriteriaText { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Source { get; set; }

    // Navigation properties
    public RegulatoryProfile RegulatoryProfile { get; set; } = null!;
}
