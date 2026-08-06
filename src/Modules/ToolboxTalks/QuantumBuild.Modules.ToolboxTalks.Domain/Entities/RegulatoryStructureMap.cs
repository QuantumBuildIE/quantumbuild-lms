using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// The ground-truth structure of one regulatory document: every principle, standard, and feature
/// it defines. DB-backed so that future uploaded documents can get a runtime-generated draft map
/// (a static-code map can only ever serve developer-hardcoded documents) and so verification state
/// — who verified this map, when — has somewhere durable to live. One map per RegulatoryDocument
/// (see the unique index on RegulatoryDocumentId in RegulatoryStructureMapConfiguration).
/// System-managed (no TenantId) — inherits from BaseEntity, matching RegulatoryDocument.
///
/// Verification state: new/generated maps are Draft. Status only becomes Verified through an
/// explicit verify action (see IRegulatoryStructureMapVerificationService) that records
/// VerifiedBy/VerifiedAt. Editing any feature's VerbatimText or FootnoteDefinition resets Status
/// back to Draft and clears VerifiedBy/VerifiedAt (same service) — a verified stamp can never sit
/// on since-changed content.
/// </summary>
public class RegulatoryStructureMap : BaseEntity
{
    public Guid RegulatoryDocumentId { get; set; }

    public RegulatoryStructureMapStatus Status { get; set; } = RegulatoryStructureMapStatus.Draft;

    /// <summary>Name/identity of whoever verified this map. Null while Status is Draft.</summary>
    public string? VerifiedBy { get; set; }

    /// <summary>When this map was verified. Null while Status is Draft.</summary>
    public DateTimeOffset? VerifiedAt { get; set; }

    // Navigation properties
    public RegulatoryDocument RegulatoryDocument { get; set; } = null!;
    public ICollection<RegulatoryStructureMapPrinciple> Principles { get; set; } = new List<RegulatoryStructureMapPrinciple>();
}
