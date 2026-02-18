using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Features.Tenants;
using QuantumBuild.Core.Application.Features.Tenants.DTOs;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/tenants")]
[Authorize(Policy = "Tenant.Manage")]
public class TenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantOnboardingService _onboardingService;

    public TenantsController(ITenantService tenantService, ITenantOnboardingService onboardingService)
    {
        _tenantService = tenantService;
        _onboardingService = onboardingService;
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
            return BadRequest(result);

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
            return NotFound(result);

        return Ok(result);
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTenantStatusCommand command)
    {
        var result = await _tenantService.UpdateStatusAsync(id, command);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }
}
