using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;

/// <summary>
/// One standard within a principle (e.g. "2.3"), holding every person-experience and provider
/// feature the document defines for it. Item count per block is document-native and may be
/// asymmetric (e.g. HIQA Standard 4.1: 5 person / 8 provider) — never inferred, balanced, or
/// treated as a transcription error to correct.
/// </summary>
public sealed record StructureStandard(
    string Id,
    IReadOnlyList<StructureFeature> Features)
{
    public IEnumerable<StructureFeature> PersonFeatures => Features.Where(f => f.Block == RequirementBlock.Person);

    public IEnumerable<StructureFeature> ProviderFeatures => Features.Where(f => f.Block == RequirementBlock.Provider);
}
