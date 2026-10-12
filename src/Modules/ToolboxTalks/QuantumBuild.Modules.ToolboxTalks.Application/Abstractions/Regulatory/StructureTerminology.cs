using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;

/// <summary>
/// The words a document uses for its own structure, so the ingestion prompt speaks the document's
/// language (HIQA: Principle / Standard; RSA: Chapter / Article). An empty block description means
/// the document has no such block and the prompt omits it.
/// </summary>
public sealed record StructureTerminology(
    string PrincipleLabel,
    string StandardLabel,
    string PersonBlockDescription,
    string ProviderBlockDescription)
{
    /// <summary>HIQA's wording; also the default for maps that declare nothing else.</summary>
    public static StructureTerminology Hiqa { get; } = new(
        RegulatoryStructureMap.DefaultPrincipleLabel,
        RegulatoryStructureMap.DefaultStandardLabel,
        RegulatoryStructureMap.DefaultPersonBlockDescription,
        RegulatoryStructureMap.DefaultProviderBlockDescription);

    public bool HasPersonBlock => !string.IsNullOrWhiteSpace(PersonBlockDescription);

    public bool HasProviderBlock => !string.IsNullOrWhiteSpace(ProviderBlockDescription);
}
