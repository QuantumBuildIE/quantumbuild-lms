using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// System-managed regulatory body entity (no TenantId).
/// e.g. HIQA, HSA, FSAI, RSA.
/// </summary>
public class RegulatoryBody : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string Country { get; set; } = string.Empty;

    public string? Website { get; set; }

    // Navigation properties
    public ICollection<RegulatoryDocument> Documents { get; set; } = new List<RegulatoryDocument>();
}
