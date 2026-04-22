using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Constants;
using QuantumBuild.Core.Application.Features.Tenants;
using QuantumBuild.Core.Application.Features.Tenants.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize(Policy = "Tenant.Manage")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantOnboardingService _onboardingService;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemAuditLogger _auditLogger;
    private readonly ILogger<TenantsController> _logger;

    public TenantsController(
        ITenantService tenantService,
        ITenantOnboardingService onboardingService,
        IMediator mediator,
        ICurrentUserService currentUserService,
        ISystemAuditLogger auditLogger,
        ILogger<TenantsController> logger)
    {
        _tenantService = tenantService;
        _onboardingService = onboardingService;
        _mediator = mediator;
        _currentUserService = currentUserService;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetPaginated(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null)
    {
        var result = await _tenantService.GetPaginatedAsync(pageNumber, pageSize, sortColumn, sortDirection, search);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _tenantService.GetByIdAsync(id);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTenantCommand command)
    {
        var result = await _tenantService.CreateAsync(command);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.Tenant.Create, success: false,
                entityType: "Tenant", failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return BadRequest(result);
        }

        await _auditLogger.LogAsync(AuditActions.Tenant.Create, success: true,
            entityType: "Tenant", entityId: result.Data!.Id, entityDisplayName: result.Data!.Name);

        if (!string.IsNullOrWhiteSpace(command.ContactEmail) && !string.IsNullOrWhiteSpace(command.ContactName))
        {
            var onboardingResult = await _onboardingService.ProvisionTenantAsync(
                result.Data!.Id, command.ContactEmail, command.ContactName);

            if (!onboardingResult.Success)
                return StatusCode(207, new
                {
                    result.Success,
                    result.Data,
                    OnboardingErrors = onboardingResult.Errors,
                    Message = "Tenant created but onboarding had errors."
                });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTenantCommand command)
    {
        var result = await _tenantService.UpdateAsync(id, command);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.Tenant.Update, success: false,
                entityType: "Tenant", entityId: id,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return NotFound(result);
        }

        await _auditLogger.LogAsync(AuditActions.Tenant.Update, success: true,
            entityType: "Tenant", entityId: id, entityDisplayName: result.Data!.Name);
        return Ok(result);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTenantStatusCommand command)
    {
        var result = await _tenantService.UpdateStatusAsync(id, command);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.Tenant.StatusUpdate, success: false,
                entityType: "Tenant", entityId: id,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return NotFound(result);
        }

        await _auditLogger.LogAsync(AuditActions.Tenant.StatusUpdate, success: true,
            entityType: "Tenant", entityId: id,
            metadataJson: JsonSerializer.Serialize(new { status = command.Status.ToString() }));
        return Ok(result);
    }

    [HttpPost("{id:guid}/reset-data")]
    public async Task<IActionResult> ResetData(Guid id)
    {
        var tenant = await _tenantService.GetByIdAsync(id);
        if (!tenant.Success)
        {
            await _auditLogger.LogAsync(AuditActions.Tenant.Reset, success: false,
                entityType: "Tenant", entityId: id, failureReason: "Tenant not found",
                metadataJson: JsonSerializer.Serialize(new { resetAt = DateTimeOffset.UtcNow }));
            return NotFound(tenant);
        }

        var result = await _mediator.Send(new ResetTenantDataCommand(id));
        var metadata = JsonSerializer.Serialize(new { resetAt = DateTimeOffset.UtcNow });

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.Tenant.Reset, success: false,
                entityType: "Tenant", entityId: id, entityDisplayName: tenant.Data!.Name,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message,
                metadataJson: metadata);
            return BadRequest(result);
        }

        await _auditLogger.LogAsync(AuditActions.Tenant.Reset, success: true,
            entityType: "Tenant", entityId: id, entityDisplayName: tenant.Data!.Name,
            metadataJson: metadata);

        _logger.LogInformation("Tenant {TenantId} data reset by {UserId}", id, _currentUserService.UserId);
        return Ok(new { message = "Tenant data reset successfully", tenantId = id });
    }
}
