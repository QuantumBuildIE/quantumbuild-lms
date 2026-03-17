using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for regulatory score assessments on translation validation runs
/// </summary>
[ApiController]
[Route("api/toolbox-talks/validation-runs/{runId:guid}/regulatory-score")]
[Authorize(Policy = "Learnings.View")]
public class RegulatoryScoreController : ControllerBase
{
    private readonly IRegulatoryScoreService _regulatoryScoreService;
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RegulatoryScoreController> _logger;

    public RegulatoryScoreController(
        IRegulatoryScoreService regulatoryScoreService,
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<RegulatoryScoreController> logger)
    {
        _regulatoryScoreService = regulatoryScoreService;
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Run a regulatory score assessment on a validation run
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(RegulatoryScoreResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Score(
        Guid runId,
        [FromBody] RegulatoryScoreRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var run = await _dbContext.TranslationValidationRuns
                .FirstOrDefaultAsync(r => r.Id == runId && r.TenantId == tenantId, cancellationToken);

            if (run == null)
                return NotFound(new { message = "Validation run not found" });

            // Check run has completed sections
            var hasCompletedSections = await _dbContext.TranslationValidationResults
                .AnyAsync(r => r.ValidationRunId == runId && r.FinalScore > 0, cancellationToken);

            if (!hasCompletedSections)
                return BadRequest(new { message = "Validation run has no completed sections" });

            // RegulatoryTranslation requires a SectorKey
            if (request.ScoreType == ValidationScoreType.RegulatoryTranslation
                && string.IsNullOrWhiteSpace(run.SectorKey))
            {
                return BadRequest(new { message = "RegulatoryTranslation scoring requires the validation run to have a SectorKey" });
            }

            var result = await _regulatoryScoreService.ScoreAsync(runId, request.ScoreType, cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Score request failed for run {RunId}: {Message}", runId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scoring validation run {RunId}", runId);
            return StatusCode(500, new { message = "Error scoring validation run" });
        }
    }

    /// <summary>
    /// Get the full score history for a validation run
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(RegulatoryScoreHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        Guid runId,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var runExists = await _dbContext.TranslationValidationRuns
                .AnyAsync(r => r.Id == runId && r.TenantId == tenantId, cancellationToken);

            if (!runExists)
                return NotFound(new { message = "Validation run not found" });

            var history = await _regulatoryScoreService.GetScoreHistoryAsync(runId, cancellationToken);

            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving score history for run {RunId}", runId);
            return StatusCode(500, new { message = "Error retrieving score history" });
        }
    }
}
