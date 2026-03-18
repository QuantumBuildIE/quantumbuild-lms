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
}
