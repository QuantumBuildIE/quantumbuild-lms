using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Junction entity linking a Tenant to a Sector.
/// Composite unique index on {TenantId, SectorId}.
/// </summary>
public class TenantSector : TenantEntity
{
    public Guid SectorId { get; set; }

    /// <summary>
    /// Whether this is the tenant's primary/default sector
    /// </summary>
    public bool IsDefault { get; set; }

    // Navigation properties
    public Sector Sector { get; set; } = null!;
}
