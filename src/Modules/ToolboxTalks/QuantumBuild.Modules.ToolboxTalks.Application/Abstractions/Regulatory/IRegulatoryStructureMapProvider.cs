namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;

/// <summary>
/// Resolves the per-document structure map that drives faithful extraction and completeness
/// checking, reading from the DB-backed RegulatoryStructureMap.
///
/// Dispatch key is <c>RegulatoryDocument.Id</c>, not <c>RegulatoryBody.Code</c> as sub-chunk 1
/// used. Reasoning (settled in the sub-chunk 2 prompt, section B): a body can have more than one
/// document, and a body-code key silently conflates them onto one map; the long-term goal is a
/// map generated per uploaded document, which only a document-level key can support. Keying by
/// document also removes the sub-chunk 1 limitation of needing <c>RegulatoryBody</c> preloaded —
/// callers now pass the id directly.
///
/// Both loud states this returns are first-class, never silent:
///   - no_structure_map: no RegulatoryStructureMap row exists for this document at all
///     (GetStructureMapAsync throws RegulatoryStructureMapNotFoundException; TryGetStructureMapAsync
///     returns Found = false).
///   - unverified: a map exists but its Status is Draft. Both methods still return/throw based
///     purely on presence — an unverified map is not "missing". Callers that need to distinguish
///     draft from verified must check the returned DocumentStructureMap.Status/IsVerified
///     themselves; this provider does not silently promote or hide draft content.
/// </summary>
public interface IRegulatoryStructureMapProvider
{
    /// <summary>Non-throwing lookup. Returns Found = false and a null map when nothing is registered for this document.</summary>
    Task<(bool Found, DocumentStructureMap? Map)> TryGetStructureMapAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The loud-failure path: throws <see cref="RegulatoryStructureMapNotFoundException"/> when
    /// nothing is registered for this document, instead of returning null for a caller to
    /// silently ignore.
    /// </summary>
    Task<DocumentStructureMap> GetStructureMapAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default);
}
