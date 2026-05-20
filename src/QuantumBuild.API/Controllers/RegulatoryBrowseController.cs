using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Tenant-admin browse endpoint for approved regulatory requirements.
/// Separate from RegulatoryIngestionController so Learnings.Admin users
/// are not blocked by the SuperUser-only Tenant.Manage gate on that controller.
/// </summary>
[ApiController]
[Route("api/regulatory")]
[Authorize(Policy = "Learnings.Admin")]
public class RegulatoryBrowseController : ControllerBase
{
    private readonly IRequirementIngestionService _ingestionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RegulatoryBrowseController> _logger;

    public RegulatoryBrowseController(
        IRequirementIngestionService ingestionService,
        ICurrentUserService currentUserService,
        ILogger<RegulatoryBrowseController> logger)
    {
        _ingestionService = ingestionService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Approved requirements filtered to the caller's assigned sectors.
    /// Drafts and Rejected requirements are excluded.
    /// </summary>
    [HttpGet("browse")]
    [ProducesResponseType(typeof(List<RegulatoryBrowseBodyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Browse(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;
            var result = await _ingestionService.GetBrowsableRequirementsAsync(tenantId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing regulatory requirements for tenant {TenantId}", _currentUserService.TenantId);
            return StatusCode(500, new { message = "Error retrieving regulatory requirements" });
        }
    }
}
