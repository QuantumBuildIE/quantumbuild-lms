using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// System-managed regulatory body entity (no TenantId).
/// e.g. HIQA, HSA, FSAI, RSA (Kind = Regulation).
/// </summary>
public class RegulatoryBody : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string? Website { get; set; }

    /// <summary>
    /// Regulation (legally mandated, applies via sector automatically) or Standard (voluntary, tenant-subscribed).
    /// </summary>
    public RegulatoryBodyKind Kind { get; set; } = RegulatoryBodyKind.Regulation;

    /// <summary>
    /// Sector this body applies to. Null for Regulation bodies (they use the RegulatoryProfile chain
    /// instead). Required for Standard bodies — see <see cref="ValidateSectorConsistency"/>.
    /// </summary>
    public Guid? SectorId { get; set; }

    // Navigation properties
    public ICollection<RegulatoryDocument> Documents { get; set; } = new List<RegulatoryDocument>();
    public Sector? Sector { get; set; }

    /// <summary>
    /// Enforces the Kind/SectorId invariant: Standard bodies must carry a SectorId, Regulation bodies must not.
    /// </summary>
    public void ValidateSectorConsistency()
    {
        if (Kind == RegulatoryBodyKind.Standard && SectorId is null)
            throw new InvalidOperationException("Standard regulatory bodies must specify a SectorId.");

        if (Kind == RegulatoryBodyKind.Regulation && SectorId is not null)
            throw new InvalidOperationException("Regulation regulatory bodies must not specify a SectorId.");
    }
}
