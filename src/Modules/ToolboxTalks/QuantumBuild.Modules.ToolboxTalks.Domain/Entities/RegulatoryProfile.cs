using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// System-managed intersection of a RegulatoryDocument and a Sector.
/// Holds scoring category weights for that combination.
/// Composite unique index on {RegulatoryDocumentId, SectorId}.
/// SectorKey is a denormalised copy of Sector.Key for quick lookup.
/// </summary>
public class RegulatoryProfile : BaseEntity
{
    public Guid RegulatoryDocumentId { get; set; }

    public Guid SectorId { get; set; }

    public string SectorKey { get; set; } = string.Empty;

    public string ScoreLabel { get; set; } = string.Empty;

    public string ExportLabel { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// JSON-serialised array of {Key, Label, Weight} objects representing
    /// the scoring categories and their weights for this profile.
    /// </summary>
    public string CategoryWeightsJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public RegulatoryDocument RegulatoryDocument { get; set; } = null!;
    public Sector Sector { get; set; } = null!;
    public ICollection<RegulatoryCriteria> Criteria { get; set; } = new List<RegulatoryCriteria>();
}
