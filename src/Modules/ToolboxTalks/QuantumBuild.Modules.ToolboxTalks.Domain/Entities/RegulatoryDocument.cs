using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// System-managed regulatory document entity (no TenantId).
/// e.g. "Draft National Standards for Home Support Services".
/// </summary>
public class RegulatoryDocument : BaseEntity
{
    public Guid RegulatoryBodyId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public DateOnly? EffectiveDate { get; set; }

    public string? Source { get; set; }

    public string? SourceUrl { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Timestamp of the last successful AI ingestion of requirements from this document.
    /// </summary>
    public DateTimeOffset? LastIngestedAt { get; set; }

    // Navigation properties
    public RegulatoryBody RegulatoryBody { get; set; } = null!;
    public ICollection<RegulatoryProfile> Profiles { get; set; } = new List<RegulatoryProfile>();
}
