using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;

/// <summary>
/// Service for AI-powered ingestion of regulatory requirements from document URLs.
/// Orchestrates the fetch → extract → draft → review → approve pipeline.
/// </summary>
public interface IRequirementIngestionService
{
    /// <summary>
    /// Starts an AI ingestion job for a regulatory document.
    /// Updates SourceUrl if provided, enqueues a Hangfire background job.
    /// </summary>
    Task<IngestionSessionDto> StartIngestionAsync(
        Guid regulatoryDocumentId,
        string sourceUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current ingestion status for a regulatory document.
    /// </summary>
    Task<IngestionSessionDto> GetIngestionStatusAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all draft requirements for a regulatory document across all its profiles.
    /// </summary>
    Task<List<DraftRequirementDto>> GetDraftRequirementsAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a draft requirement, optionally updating fields before approval.
    /// </summary>
    Task<RegulatoryRequirementDto> ApproveRequirementAsync(
        Guid requirementId,
        ApproveRequirementRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a draft requirement with notes.
    /// </summary>
    Task RejectRequirementAsync(
        Guid requirementId,
        string notes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a draft requirement's fields without changing its status.
    /// </summary>
    Task<RegulatoryRequirementDto> UpdateDraftRequirementAsync(
        Guid requirementId,
        UpdateDraftRequirementRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves all draft requirements for a regulatory document. Returns the count approved.
    /// </summary>
    Task<int> ApproveAllDraftRequirementsAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all regulatory documents with their body, profiles, and requirement counts.
    /// </summary>
    Task<List<RegulatoryDocumentListDto>> GetDocumentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns approved regulatory requirements scoped to the tenant's assigned sectors.
    /// Drafts and Rejected requirements are excluded — mid-workflow data invisible to tenant admins.
    /// </summary>
    Task<List<RegulatoryBrowseBodyDto>> GetBrowsableRequirementsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a source PDF for a regulatory document to storage and updates its SourceUrl.
    /// Does NOT trigger ingestion — that remains an explicit separate action.
    /// Returns null if the document does not exist.
    /// </summary>
    Task<RegulatoryDocumentUploadResponseDto?> UploadSourceDocumentAsync(
        Guid regulatoryDocumentId,
        Stream fileContent,
        string originalFileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists regulatory bodies for the admin catalog / document-creation body picker.
    /// Pass <paramref name="kind"/> to filter to Regulation-only or Standard-only bodies.
    /// </summary>
    Task<List<RegulatoryBodyDto>> GetRegulatoryBodiesAsync(
        Domain.Enums.RegulatoryBodyKind? kind = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new regulatory document. Persists with LastIngestionStatus=Idle and no
    /// profiles — ingestion and sector-profile setup remain separate, later actions.
    /// </summary>
    Task<RegulatoryDocumentListDto> CreateDocumentAsync(
        CreateRegulatoryDocumentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new regulatory body (catalog entry) — a Regulation or a Standard. Enforces the
    /// Kind/SectorId invariant at the handler layer, in addition to the DB check constraint.
    /// </summary>
    Task<RegulatoryBodyDto> CreateRegulatoryBodyAsync(
        CreateRegulatoryBodyRequest request,
        CancellationToken cancellationToken = default);
}
