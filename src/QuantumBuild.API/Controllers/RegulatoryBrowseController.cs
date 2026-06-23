using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

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
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RegulatoryBrowseController> _logger;

    public RegulatoryBrowseController(
        IRequirementIngestionService ingestionService,
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<RegulatoryBrowseController> logger)
    {
        _ingestionService = ingestionService;
        _dbContext = dbContext;
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

    /// <summary>
    /// Lightweight pre-flight check: does a sector key have an ingested regulatory profile
    /// with approved requirements? Used by the wizard translate step and the results screen.
    /// Bogus sector keys return HasRegulatoryProfile=false — uniform shape for the frontend.
    /// </summary>
    [HttpGet("applicability")]
    [ProducesResponseType(typeof(RegulatoryApplicabilityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegulatoryApplicabilityDto>> GetApplicability(
        [FromQuery] string sectorKey,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sectorKey))
            return BadRequest("sectorKey is required");

        var profiles = await _dbContext.RegulatoryProfiles
            .Where(p => p.SectorKey == sectorKey)
            .ToListAsync(ct);

        if (profiles.Count == 0)
            return Ok(new RegulatoryApplicabilityDto
            {
                HasRegulatoryProfile = false,
                ApprovedRequirementCount = 0,
                ProfileName = null
            });

        var profileIds = profiles.Select(p => p.Id).ToList();
        var approvedCount = await _dbContext.RegulatoryRequirements
            .IgnoreQueryFilters()
            .Where(r => !r.IsDeleted
                     && r.IngestionStatus == RequirementIngestionStatus.Approved
                     && profileIds.Contains(r.RegulatoryProfileId))
            .CountAsync(ct);

        return Ok(new RegulatoryApplicabilityDto
        {
            HasRegulatoryProfile = true,
            ApprovedRequirementCount = approvedCount,
            ProfileName = profiles.Count == 1 ? profiles[0].ScoreLabel : null
        });
    }
}
