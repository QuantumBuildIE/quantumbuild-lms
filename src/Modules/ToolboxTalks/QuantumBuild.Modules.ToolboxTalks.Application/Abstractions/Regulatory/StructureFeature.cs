using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;

/// <summary>
/// One document-native feature: a verbatim, identifier-tagged unit of content within a standard.
/// <see cref="Identifier"/> is stored exactly as printed in the source document (e.g. HIQA
/// Standard 4.5's identifiers keep their trailing period, "4.5.1.", and Standard 2.3's provider
/// block starts at "2.3.2" with no "2.3.1") — no normalisation, no positional inference, per the
/// settled preserve-as-is decision. Bundled multi-obligation features (several distinct claims in
/// one printed item, e.g. HIQA person feature 1.1.8) are stored as a single feature, matching the
/// document's own choice not to sub-number them.
/// </summary>
public sealed record StructureFeature(
    string Identifier,
    RequirementBlock Block,
    string VerbatimText,
    string? FootnoteDefinition = null);
