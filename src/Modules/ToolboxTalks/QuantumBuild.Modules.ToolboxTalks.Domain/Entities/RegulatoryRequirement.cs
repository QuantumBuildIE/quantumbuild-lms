using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// A specific compliance obligation within a regulatory profile.
/// System-managed (no TenantId) — inherits from BaseEntity.
/// IngestionStatus gates visibility: only Approved requirements are shown to tenants.
/// </summary>
public class RegulatoryRequirement : BaseEntity
{
    public Guid RegulatoryProfileId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Section { get; set; }

    public string? SectionLabel { get; set; }

    public string? Principle { get; set; }

    public string? PrincipleLabel { get; set; }

    public string Priority { get; set; } = "med";

    public int DisplayOrder { get; set; }

    public RequirementIngestionSource IngestionSource { get; set; } = RequirementIngestionSource.Manual;

    public RequirementIngestionStatus IngestionStatus { get; set; } = RequirementIngestionStatus.Draft;

    /// <summary>
    /// Reviewer notes on ingestion decision
    /// </summary>
    public string? IngestionNotes { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The source document's own identifier for the feature this requirement transcribes (e.g.
    /// "1.1.7", "4.5.1." — trailing punctuation and other source-native formatting is preserved
    /// literally, never normalised). Distinct from <see cref="Section"/>, which is a
    /// standard-level reference only (e.g. "Standard 1.1") and is read as opaque display text by
    /// existing consumers (RequirementIngestionService, RequirementMappingService,
    /// InspectionReportService) — adding this field alongside Section rather than repurposing it
    /// keeps those consumers unaffected. Null for requirements not sourced from a per-document
    /// structure map (e.g. manually created requirements).
    /// </summary>
    public string? FeatureIdentifier { get; set; }

    /// <summary>
    /// Which block (person-experience vs provider) the source feature belongs to. The two blocks
    /// are independently numbered per standard, so this is what disambiguates two requirements
    /// that share the same <see cref="FeatureIdentifier"/>. Null for requirements not sourced from
    /// a per-document structure map.
    /// </summary>
    public RequirementBlock? Block { get; set; }

    /// <summary>
    /// The feature's authoritative content, transcribed verbatim from the source document — not a
    /// paraphrase. Distinct from <see cref="Description"/>, which is documented and used
    /// elsewhere as a model-authored gloss of what the requirement entails; giving verbatim text
    /// its own field avoids silently redefining Description's existing contract. A future display
    /// label (not added in this chunk) would live on <see cref="Title"/> once Title stops being
    /// treated as authoritative content.
    /// </summary>
    public string? VerbatimText { get; set; }

    /// <summary>
    /// Verbatim text of a footnote the source document attaches to this feature (e.g. a defined
    /// term like "decision supporter" or "collection agent"). Null when the feature carries no
    /// footnote.
    /// </summary>
    public string? FootnoteDefinition { get; set; }

    // Navigation properties
    public RegulatoryProfile RegulatoryProfile { get; set; } = null!;
    public ICollection<RegulatoryRequirementMapping> Mappings { get; set; } = new List<RegulatoryRequirementMapping>();
}
