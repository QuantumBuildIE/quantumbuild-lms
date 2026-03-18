using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Sectors;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Sectors;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/tenants/{tenantId:guid}/sectors")]
[Authorize]
public class TenantSectorsController(
    ITenantSectorService tenantSectorService,
    IValidator<AssignTenantSectorRequest> validator,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// Get all active sectors assigned to a tenant.
    /// Any authenticated user can read their own tenant's sectors.
    /// SuperUsers can read any tenant's sectors.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TenantSectorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenantSectors(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsSuperUser && currentUserService.TenantId != tenantId)
            return Forbid();

        var sectors = await tenantSectorService.GetTenantSectorsAsync(tenantId, cancellationToken);
        return Ok(sectors);
    }

    /// <summary>
    /// Assign a sector to a tenant (restore-on-reassign if previously soft-deleted)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Tenant.Manage")]
    [ProducesResponseType(typeof(TenantSectorDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignSector(
        Guid tenantId,
        [FromBody] AssignTenantSectorRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return BadRequest(new { errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        try
        {
            var result = await tenantSectorService.AssignSectorAsync(
                tenantId, request.SectorId, request.IsDefault, cancellationToken);

            return CreatedAtAction(nameof(GetTenantSectors), new { tenantId }, result);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("CONFLICT:"))
        {
            return Conflict(new { error = ex.Message.Replace("CONFLICT:", "") });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Remove a sector from a tenant (soft delete)
    /// </summary>
    [HttpDelete("{sectorId:guid}")]
    [Authorize(Policy = "Tenant.Manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSector(Guid tenantId, Guid sectorId, CancellationToken cancellationToken)
    {
        try
        {
            await tenantSectorService.RemoveSectorAsync(tenantId, sectorId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("VALIDATION:"))
        {
            return BadRequest(new { error = ex.Message.Replace("VALIDATION:", "") });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Set a sector as the default for a tenant
    /// </summary>
    [HttpPut("{sectorId:guid}/set-default")]
    [Authorize(Policy = "Tenant.Manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDefault(Guid tenantId, Guid sectorId, CancellationToken cancellationToken)
    {
        try
        {
            await tenantSectorService.SetDefaultSectorAsync(tenantId, sectorId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get the default sector for a tenant (used by TransVal wizard auto-population).
    /// Any authenticated user can read their own tenant's default sector.
    /// SuperUsers can read any tenant's default sector.
    /// </summary>
    [HttpGet("default")]
    [ProducesResponseType(typeof(TenantSectorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDefault(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsSuperUser && currentUserService.TenantId != tenantId)
            return Forbid();

        var result = await tenantSectorService.GetDefaultSectorAsync(tenantId, cancellationToken);

        if (result == null)
            return NotFound(new { error = "No default sector found. Multiple sectors assigned with no default set." });

        return Ok(result);
    }
}
