using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Abstractions;
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

        // Detect if QrLocationTrainingEnabled is being turned on for the first time
        var qrSetting = dto.Settings.FirstOrDefault(s => s.Key == TenantSettingKeys.QrLocationTrainingEnabled);
        bool shouldEnqueuePinJob = false;

        if (qrSetting?.Value == "true")
        {
            var previous = await tenantSettingsService.GetSettingAsync(
                tenantId, TenantSettingKeys.QrLocationTrainingEnabled, ct: ct);

            if (previous != "true")
                shouldEnqueuePinJob = true;
        }

        foreach (var setting in dto.Settings)
        {
            await tenantSettingsService.SetSettingAsync(tenantId, setting.Key, setting.Value, ct);
        }

        if (shouldEnqueuePinJob)
        {
            BackgroundJob.Enqueue<IGenerateEmployeePinsJob>(
                j => j.ExecuteAsync(tenantId, CancellationToken.None));
        }

        var settings = await tenantSettingsService.GetAllSettingsAsync(tenantId, ct);
        return Ok(settings);
    }
}
