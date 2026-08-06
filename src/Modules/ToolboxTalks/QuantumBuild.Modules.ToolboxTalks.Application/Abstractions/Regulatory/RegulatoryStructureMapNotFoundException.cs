namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;

/// <summary>
/// Thrown by <see cref="IRegulatoryStructureMapProvider.GetStructureMapAsync"/> when no structure map
/// is registered for a document. Extraction has nothing to drive it or check completeness against
/// without one — per the settled decision, a document with no map must fail loudly, never
/// silently no-op the way today's FindMissingStandards fallback does for an unmapped principle
/// number. <see cref="ErrorCode"/> mirrors the existing untyped-string convention already used for
/// RegulatoryDocument.LastIngestionErrorCode (e.g. "invalid_uri", "fetch_failed" in
/// RequirementIngestionJob) so a future catch site can set LastIngestionErrorCode = ErrorCode
/// directly, without introducing a second, inconsistent error-code shape.
/// </summary>
public sealed class RegulatoryStructureMapNotFoundException : Exception
{
    public const string ErrorCode = "no_structure_map";

    public Guid RegulatoryDocumentId { get; }

    public RegulatoryStructureMapNotFoundException(Guid regulatoryDocumentId, string documentTitle)
        : base($"No structure map is registered for regulatory document '{documentTitle}' ({regulatoryDocumentId}). " +
               "Extraction cannot proceed without a per-document structure map — see IRegulatoryStructureMapProvider.")
    {
        RegulatoryDocumentId = regulatoryDocumentId;
    }
}
