using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Translation Pipeline Audit — deviations, module outcomes, change records, and dashboard.
/// Route base: /api/toolbox-talks/pipeline
/// </summary>
[ApiController]
[Route("api/toolbox-talks/pipeline")]
[Authorize(Policy = "Learnings.View")]
public class PipelineAuditController : ControllerBase
{
    private readonly ITranslationDeviationService _deviationService;
    private readonly IPipelineAuditQueryService _auditQueryService;
    private readonly IPipelineVersionService _pipelineVersionService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<PipelineAuditController> _logger;

    public PipelineAuditController(
        ITranslationDeviationService deviationService,
        IPipelineAuditQueryService auditQueryService,
        IPipelineVersionService pipelineVersionService,
        ICurrentUserService currentUser,
        ILogger<PipelineAuditController> logger)
    {
        _deviationService = deviationService;
        _auditQueryService = auditQueryService;
        _pipelineVersionService = pipelineVersionService;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ─── Dashboard ────────────────────────────────────────────────────────────

    /// <summary>
    /// Summary dashboard — deviation counts, change records, locked terms, active pipeline.
    /// SuperUser may pass X-Tenant-Id header for tenant-scoped counts.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(PipelineAuditDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        try
        {
            Guid? tenantOverride = null;
            if (_currentUser.IsSuperUser && Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue)
                && Guid.TryParse(headerValue.ToString(), out var parsedTenantId))
            {
                tenantOverride = parsedTenantId;
            }

            var dashboard = await _auditQueryService.GetDashboardSummaryAsync(tenantOverride, cancellationToken);
            return Ok(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading pipeline audit dashboard");
            return StatusCode(500, Result.Fail("Error loading dashboard"));
        }
    }

    // ─── Module Outcomes ─────────────────────────────────────────────────────

    /// <summary>
    /// Paginated list of completed validation runs as module outcomes.
    /// </summary>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(PaginatedList<ModuleOutcomeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModuleOutcomes(
        [FromQuery] string? outcome,
        [FromQuery] string? languageCode,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ValidationOutcome? parsedOutcome = null;
            if (!string.IsNullOrEmpty(outcome) && Enum.TryParse<ValidationOutcome>(outcome, out var parsed))
                parsedOutcome = parsed;

            Guid? tenantOverride = null;
            if (_currentUser.IsSuperUser && Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue)
                && Guid.TryParse(headerValue.ToString(), out var parsedTenantId))
            {
                tenantOverride = parsedTenantId;
            }

            var result = await _auditQueryService.GetModuleOutcomesAsync(
                tenantOverride, parsedOutcome, languageCode, page, pageSize, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading module outcomes");
            return StatusCode(500, Result.Fail("Error loading module outcomes"));
        }
    }

    // ─── Deviations ───────────────────────────────────────────────────────────

    /// <summary>
    /// Paginated list of deviations for the current tenant.
    /// </summary>
    [HttpGet("deviations")]
    [ProducesResponseType(typeof(PaginatedList<TranslationDeviationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDeviations(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DeviationStatus? parsedStatus = null;
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<DeviationStatus>(status, out var parsed))
                parsedStatus = parsed;

            var result = await _deviationService.GetPagedAsync(parsedStatus, page, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading deviations");
            return StatusCode(500, Result.Fail("Error loading deviations"));
        }
    }

    /// <summary>
    /// Get a single deviation by ID.
    /// </summary>
    [HttpGet("deviations/{id:guid}")]
    [ProducesResponseType(typeof(TranslationDeviationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeviation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var dto = await _deviationService.GetByIdAsync(id, cancellationToken);
            return dto == null ? NotFound() : Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading deviation {Id}", id);
            return StatusCode(500, Result.Fail("Error loading deviation"));
        }
    }

    /// <summary>
    /// Create a new deviation record.
    /// </summary>
    [HttpPost("deviations")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(TranslationDeviationDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDeviation(
        [FromBody] CreateDeviationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Nature))
                return BadRequest(new { message = "Nature is required" });

            if (string.IsNullOrWhiteSpace(request.RootCauseCategory))
                return BadRequest(new { message = "RootCauseCategory is required" });

            var dto = await _deviationService.CreateAsync(request, cancellationToken);

            _logger.LogInformation(
                "Deviation {DeviationId} created by {User}", dto.DeviationId, _currentUser.UserName);

            return CreatedAtAction(nameof(GetDeviation), new { id = dto.Id }, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating deviation");
            return StatusCode(500, Result.Fail("Error creating deviation"));
        }
    }

    /// <summary>
    /// Update deviation status (Open → InProgress → Closed).
    /// </summary>
    [HttpPut("deviations/{id:guid}/status")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(TranslationDeviationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDeviationStatus(
        Guid id,
        [FromBody] UpdateDeviationStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Enum.TryParse<DeviationStatus>(request.Status, out var status))
                return BadRequest(new { message = $"Invalid status '{request.Status}'" });

            var dto = await _deviationService.UpdateStatusAsync(id, status, request.ClosedBy, cancellationToken);
            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating deviation status for {Id}", id);
            return StatusCode(500, Result.Fail("Error updating deviation status"));
        }
    }

    // ─── Pipeline Changes ─────────────────────────────────────────────────────

    /// <summary>
    /// Paginated list of all pipeline change records (system-wide, append-only).
    /// </summary>
    [HttpGet("changes")]
    [ProducesResponseType(typeof(PaginatedList<PipelineChangeRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChangeRecords(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _auditQueryService.GetChangeRecordsAsync(page, pageSize, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading change records");
            return StatusCode(500, Result.Fail("Error loading change records"));
        }
    }

    /// <summary>
    /// Create a new pipeline change record (SuperUser only).
    /// Also bumps the active pipeline version.
    /// </summary>
    [HttpPost("changes")]
    [ProducesResponseType(typeof(PipelineChangeRecordDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateChangeRecord(
        [FromBody] CreatePipelineChangeRecordRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsSuperUser)
            return Forbid();

        try
        {
            if (string.IsNullOrWhiteSpace(request.Component))
                return BadRequest(new { message = "Component is required" });
            if (string.IsNullOrWhiteSpace(request.Justification))
                return BadRequest(new { message = "Justification is required" });
            if (string.IsNullOrWhiteSpace(request.NewVersionLabel))
                return BadRequest(new { message = "NewVersionLabel is required" });

            var record = await _pipelineVersionService.CreateChangeRecordAsync(request, cancellationToken);

            _logger.LogInformation(
                "Pipeline change record {ChangeId} created by SuperUser {User}",
                record.ChangeId, _currentUser.UserName);

            var dto = new PipelineChangeRecordDto
            {
                Id = record.Id,
                ChangeId = record.ChangeId,
                Component = record.Component,
                ChangeFrom = record.ChangeFrom,
                ChangeTo = record.ChangeTo,
                Justification = record.Justification,
                ImpactAssessment = record.ImpactAssessment,
                PriorModulesAction = record.PriorModulesAction,
                Approver = record.Approver,
                DeployedAt = record.DeployedAt,
                PipelineVersionId = record.PipelineVersionId,
                PreviousPipelineVersionId = record.PreviousPipelineVersionId,
                CreatedAt = record.CreatedAt
            };

            return CreatedAtAction(nameof(GetChangeRecords), dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pipeline change record");
            return StatusCode(500, Result.Fail("Error creating change record"));
        }
    }

    // ─── Pipeline Version ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the currently active pipeline version.
    /// </summary>
    [HttpGet("version")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveVersion(CancellationToken cancellationToken)
    {
        try
        {
            var version = await _auditQueryService.GetActivePipelineVersionAsync(cancellationToken);
            if (version == null)
                return Ok(new { version = "—", hash = "—", computedAt = (DateTimeOffset?)null });

            return Ok(new
            {
                id = version.Id,
                version = version.Version,
                hash = version.Hash,
                computedAt = version.ComputedAt,
                componentsJson = version.ComponentsJson
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading active pipeline version");
            return StatusCode(500, Result.Fail("Error loading pipeline version"));
        }
    }
}
