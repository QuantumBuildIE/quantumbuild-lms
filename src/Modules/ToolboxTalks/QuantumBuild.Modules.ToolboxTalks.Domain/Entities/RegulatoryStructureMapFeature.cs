using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// One document-native feature: a verbatim, identifier-tagged unit of content within a
/// RegulatoryStructureMapStandard. Identifier and VerbatimText are stored exactly as printed in
/// the source document (e.g. HIQA Standard 4.5's identifiers keep their trailing period,
/// "4.5.1.", and Standard 2.3's provider block starts at "2.3.2" with no "2.3.1") — no
/// normalisation, no positional inference. Bundled multi-obligation features are stored as a
/// single feature, matching the document's own choice not to sub-number them. Block disambiguates
/// person-experience vs provider features that share the same Identifier within a standard.
/// System-managed (no TenantId) — inherits from BaseEntity.
/// </summary>
public class RegulatoryStructureMapFeature : BaseEntity
{
    public Guid RegulatoryStructureMapStandardId { get; set; }

    public string Identifier { get; set; } = string.Empty;

    public RequirementBlock Block { get; set; }

    public string VerbatimText { get; set; } = string.Empty;

    public string? FootnoteDefinition { get; set; }

    /// <summary>Ordering within a standard's block, document order.</summary>
    public int DisplayOrder { get; set; }

    // Navigation properties
    public RegulatoryStructureMapStandard RegulatoryStructureMapStandard { get; set; } = null!;
}
