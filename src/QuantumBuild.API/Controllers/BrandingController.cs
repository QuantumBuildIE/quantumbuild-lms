using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;

namespace QuantumBuild.API.Controllers;

public record CurrentBrandingResponse(string? LogoUrl, string? TenantName);

/// <summary>
/// Read-only branding for the caller's current tenant. Deliberately takes no tenant id.
/// </summary>
[ApiController]
[Route("api/branding")]
[Authorize]
public class BrandingController(
    ApplicationDbContext db,
    IR2StorageService storage,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>
    /// Branding for the current tenant: the JWT tenant for regular users, the X-Tenant-Id tenant for
    /// SuperUsers. No tenant in context (SuperUser with no header) returns a null logo and null name.
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(CurrentBrandingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var tenantId = currentUserService.TenantId;
        if (tenantId == Guid.Empty)
            return Ok(new CurrentBrandingResponse(null, null));

        var logoKey = await db.TenantBrandings
            .Where(b => b.TenantId == tenantId)
            .Select(b => b.LogoKey)
            .FirstOrDefaultAsync(cancellationToken);

        var tenantName = await db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new CurrentBrandingResponse(logoKey == null ? null : storage.GetPublicUrl(logoKey), tenantName));
    }
}
