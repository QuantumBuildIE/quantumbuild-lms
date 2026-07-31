using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;

/// <summary>
/// The ground-truth structure of one regulatory document: every principle, standard, and feature
/// it defines. This is authored/verified data, not extraction output — faithful extraction (a
/// later chunk) is validated against this map, not the other way around.
///
/// This record is the read-side projection <see cref="IRegulatoryStructureMapProvider"/> builds
/// from the DB-backed RegulatoryStructureMap entity tree — it is no longer the storage
/// representation itself (that moved to the DB so future uploaded documents can get a
/// runtime-generated draft map, and so verification state has somewhere durable to live).
/// <see cref="RegulatoryDocumentId"/> is the dispatch key <see cref="IRegulatoryStructureMapProvider"/>
/// resolves a document to. <see cref="Id"/> identifies the underlying RegulatoryStructureMap row
/// (needed by verification calls). <see cref="StructurePrinciple"/>/<see cref="StructureStandard"/>/
/// <see cref="StructureFeature"/> are unchanged from the prior static representation.
/// </summary>
public sealed record DocumentStructureMap(
    Guid Id,
    Guid RegulatoryDocumentId,
    RegulatoryStructureMapStatus Status,
    string? VerifiedBy,
    DateTimeOffset? VerifiedAt,
    IReadOnlyList<StructurePrinciple> Principles)
{
    public IEnumerable<StructureStandard> AllStandards => Principles.SelectMany(p => p.Standards);

    public IEnumerable<StructureFeature> AllFeatures => AllStandards.SelectMany(s => s.Features);

    /// <summary>
    /// True once a human has verified this map's content. False (draft/unverified) must be
    /// surfaced explicitly by callers — an unverified map is not "missing" (see
    /// IRegulatoryStructureMapProvider), but it also isn't yet confirmed ground truth.
    /// </summary>
    public bool IsVerified => Status == RegulatoryStructureMapStatus.Verified;
}
