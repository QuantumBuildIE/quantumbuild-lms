using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// SuperUser-only controller for managing regulatory document ingestion.
/// Handles AI-powered extraction of requirements from document URLs.
/// </summary>
[ApiController]
[Route("api/regulatory")]
[Authorize(Policy = "Tenant.Manage")]
public class RegulatoryIngestionController : ControllerBase
{
    private readonly IRequirementIngestionService _ingestionService;
    private readonly ILogger<RegulatoryIngestionController> _logger;

    public RegulatoryIngestionController(
        IRequirementIngestionService ingestionService,
        ILogger<RegulatoryIngestionController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    /// <summary>
    /// List all regulatory documents with body, profiles, and requirement counts
    /// </summary>
    [HttpGet("documents")]
    [ProducesResponseType(typeof(List<RegulatoryDocumentListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocuments(CancellationToken cancellationToken)
    {
        try
        {
            var documents = await _ingestionService.GetDocumentsAsync(cancellationToken);
            return Ok(documents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving regulatory documents");
            return StatusCode(500, new { message = "Error retrieving regulatory documents" });
        }
    }

    /// <summary>
    /// Start AI ingestion of requirements from a regulatory document URL
    /// </summary>
    [HttpPost("documents/{documentId:guid}/ingest")]
    [ProducesResponseType(typeof(IngestionSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StartIngestion(
        Guid documentId,
        [FromBody] StartIngestionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ingestionService.StartIngestionAsync(
                documentId, request.SourceUrl, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Ingestion start failed for document {DocumentId}", documentId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting ingestion for document {DocumentId}", documentId);
            return StatusCode(500, new { message = "Error starting ingestion" });
        }
    }

    /// <summary>
    /// Get the current ingestion status for a regulatory document
    /// </summary>
    [HttpGet("documents/{documentId:guid}/ingestion-status")]
    [ProducesResponseType(typeof(IngestionSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIngestionStatus(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ingestionService.GetIngestionStatusAsync(documentId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ingestion status for document {DocumentId}", documentId);
            return StatusCode(500, new { message = "Error getting ingestion status" });
        }
    }

    /// <summary>
    /// Get all draft requirements for a regulatory document
    /// </summary>
    [HttpGet("documents/{documentId:guid}/draft-requirements")]
    [ProducesResponseType(typeof(List<DraftRequirementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDraftRequirements(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var drafts = await _ingestionService.GetDraftRequirementsAsync(documentId, cancellationToken);
            return Ok(drafts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting draft requirements for document {DocumentId}", documentId);
            return StatusCode(500, new { message = "Error getting draft requirements" });
        }
    }

    /// <summary>
    /// Approve a draft requirement (optionally editing fields before approval)
    /// </summary>
    [HttpPut("requirements/{requirementId:guid}/approve")]
    [ProducesResponseType(typeof(RegulatoryRequirementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveRequirement(
        Guid requirementId,
        [FromBody] ApproveRequirementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ingestionService.ApproveRequirementAsync(
                requirementId, request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving requirement {RequirementId}", requirementId);
            return StatusCode(500, new { message = "Error approving requirement" });
        }
    }

    /// <summary>
    /// Reject a draft requirement with notes
    /// </summary>
    [HttpPut("requirements/{requirementId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectRequirement(
        Guid requirementId,
        [FromBody] RejectRequirementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _ingestionService.RejectRequirementAsync(
                requirementId, request.Notes, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting requirement {RequirementId}", requirementId);
            return StatusCode(500, new { message = "Error rejecting requirement" });
        }
    }

    /// <summary>
    /// Update a draft requirement without changing its status
    /// </summary>
    [HttpPut("requirements/{requirementId:guid}")]
    [ProducesResponseType(typeof(RegulatoryRequirementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDraftRequirement(
        Guid requirementId,
        [FromBody] UpdateDraftRequirementRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _ingestionService.UpdateDraftRequirementAsync(
                requirementId, request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating requirement {RequirementId}", requirementId);
            return StatusCode(500, new { message = "Error updating requirement" });
        }
    }

    /// <summary>
    /// Approve all draft requirements for a regulatory document
    /// </summary>
    [HttpPost("documents/{documentId:guid}/approve-all")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveAllDrafts(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await _ingestionService.ApproveAllDraftRequirementsAsync(
                documentId, cancellationToken);
            return Ok(new { approved = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving all drafts for document {DocumentId}", documentId);
            return StatusCode(500, new { message = "Error approving all draft requirements" });
        }
    }
}

/// <summary>
/// Request body for starting an ingestion job
/// </summary>
public record StartIngestionRequest
{
    public string SourceUrl { get; init; } = string.Empty;
}
