using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Features.TenantSettings;
using QuantumBuild.Core.Application.Features.TenantSettings.DTOs;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/tenant-settings")]
[Authorize(Policy = "Learnings.Admin")]
public class TenantSettingsController(
    ITenantSettingsService tenantSettingsService,
    ICurrentUserService currentUserService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tenantId = currentUserService.TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest("Tenant context required.");

        var settings = await tenantSettingsService.GetAllSettingsAsync(tenantId, ct);
        return Ok(settings);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateTenantSettingsDto dto, CancellationToken ct)
    {
        var tenantId = currentUserService.TenantId;
        if (tenantId == Guid.Empty)
            return BadRequest("Tenant context required.");

        foreach (var setting in dto.Settings)
        {
            await tenantSettingsService.SetSettingAsync(tenantId, setting.Key, setting.Value, ct);
        }

        var settings = await tenantSettingsService.GetAllSettingsAsync(tenantId, ct);
        return Ok(settings);
    }
}
