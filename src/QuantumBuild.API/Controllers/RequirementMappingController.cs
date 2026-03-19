using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Mapping;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Tenant admin controller for reviewing AI-suggested regulatory requirement mappings.
/// Mappings are auto-scoped to the current tenant via ICurrentUserService.
/// </summary>
[ApiController]
[Route("api/toolbox-talks/requirement-mappings")]
[Authorize(Policy = "Learnings.Admin")]
public class RequirementMappingController : ControllerBase
{
    private readonly IRequirementMappingService _mappingService;
    private readonly ILogger<RequirementMappingController> _logger;

    public RequirementMappingController(
        IRequirementMappingService mappingService,
        ILogger<RequirementMappingController> logger)
    {
        _mappingService = mappingService;
        _logger = logger;
    }

    /// <summary>
    /// Get all pending mappings with summary counts for the current tenant
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(MappingSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingMappings(CancellationToken cancellationToken)
    {
        try
        {
            var summary = await _mappingService.GetPendingMappingsAsync(cancellationToken);
            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending requirement mappings");
            return StatusCode(500, new { message = "Error retrieving pending mappings" });
        }
    }

    /// <summary>
    /// Confirm an AI-suggested mapping
    /// </summary>
    [HttpPut("{mappingId:guid}/confirm")]
    [ProducesResponseType(typeof(PendingMappingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmMapping(
        Guid mappingId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mappingService.ConfirmMappingAsync(mappingId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming mapping {MappingId}", mappingId);
            return StatusCode(500, new { message = "Error confirming mapping" });
        }
    }

    /// <summary>
    /// Reject an AI-suggested mapping
    /// </summary>
    [HttpPut("{mappingId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectMapping(
        Guid mappingId,
        [FromBody] RejectMappingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _mappingService.RejectMappingAsync(mappingId, request.Notes, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting mapping {MappingId}", mappingId);
            return StatusCode(500, new { message = "Error rejecting mapping" });
        }
    }

    /// <summary>
    /// Confirm all suggested mappings for the current tenant in one operation
    /// </summary>
    [HttpPost("confirm-all")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmAll(CancellationToken cancellationToken)
    {
        try
        {
            var count = await _mappingService.ConfirmAllSuggestedAsync(cancellationToken);
            return Ok(new { confirmed = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming all suggested mappings");
            return StatusCode(500, new { message = "Error confirming all mappings" });
        }
    }

    /// <summary>
    /// Get count of unconfirmed suggested mappings for a specific talk or course.
    /// Used by the assignment flow to show a warning banner.
    /// </summary>
    [HttpGet("unconfirmed-count")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnconfirmedCount(
        [FromQuery] Guid? toolboxTalkId,
        [FromQuery] Guid? courseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var count = await _mappingService.GetUnconfirmedCountAsync(
                toolboxTalkId, courseId, cancellationToken);
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting unconfirmed mapping count");
            return StatusCode(500, new { message = "Error getting unconfirmed count" });
        }
    }
}
