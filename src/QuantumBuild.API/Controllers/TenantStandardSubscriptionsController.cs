using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Standards;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Standards;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Tenant admin surface for subscribing to Standard-kind RegulatoryBody entries. Regulations
/// apply automatically via sector (RegulatoryProfile chain) and are not managed here.
/// </summary>
[ApiController]
[Route("api/tenants/{tenantId:guid}/standards")]
[Authorize]
public class TenantStandardSubscriptionsController(
    ITenantStandardSubscriptionService subscriptionService,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// Standards available for the tenant to subscribe to, scoped to the tenant's active
    /// sectors by default. Pass includeCrossSector=true to also list Standards tagged to
    /// other sectors.
    /// </summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(List<AvailableStandardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAvailableStandards(
        Guid tenantId,
        [FromQuery] bool includeCrossSector,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(tenantId))
            return Forbid();

        var result = await subscriptionService.GetAvailableStandardsAsync(tenantId, includeCrossSector, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// The tenant's current Standard subscriptions.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<TenantStandardSubscriptionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSubscribedStandards(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(tenantId))
            return Forbid();

        var result = await subscriptionService.GetSubscribedStandardsAsync(tenantId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Subscribe the tenant to a Standard. Restores a previously unsubscribed (soft-deleted)
    /// subscription if one exists.
    /// </summary>
    [HttpPost("{regulatoryBodyId:guid}")]
    [ProducesResponseType(typeof(TenantStandardSubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Subscribe(Guid tenantId, Guid regulatoryBodyId, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(tenantId))
            return Forbid();

        try
        {
            var result = await subscriptionService.SubscribeAsync(tenantId, regulatoryBodyId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (ex.Message == "Regulatory body not found.")
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Unsubscribe the tenant from a Standard (soft delete).
    /// </summary>
    [HttpDelete("{regulatoryBodyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unsubscribe(Guid tenantId, Guid regulatoryBodyId, CancellationToken cancellationToken)
    {
        if (!IsAuthorized(tenantId))
            return Forbid();

        try
        {
            await subscriptionService.UnsubscribeAsync(tenantId, regulatoryBodyId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Tenant admin territory: caller must hold Learnings.Admin on their own tenant, or be SuperUser.
    /// </summary>
    private bool IsAuthorized(Guid tenantId)
    {
        if (currentUserService.IsSuperUser)
            return true;

        var isLearningsAdmin = User.HasClaim("permission", "Learnings.Admin");
        var ownTenant = currentUserService.TenantId == tenantId;
        return isLearningsAdmin && ownTenant;
    }
}
