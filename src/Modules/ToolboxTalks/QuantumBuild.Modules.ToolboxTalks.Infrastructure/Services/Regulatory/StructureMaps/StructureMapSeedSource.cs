using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory.StructureMaps;

/// <summary>
/// One authored structure map plus the document it binds to. <see cref="DocumentTitle"/> is matched
/// exactly (within <see cref="RegulatoryBodyCode"/>) by RegulatoryStructureMapSeedData; there is no
/// fallback to "any document of that body".
/// </summary>
public sealed record StructureMapSeedSource(
    string RegulatoryBodyCode,
    string DocumentTitle,
    IReadOnlyList<StructurePrinciple> Principles);
