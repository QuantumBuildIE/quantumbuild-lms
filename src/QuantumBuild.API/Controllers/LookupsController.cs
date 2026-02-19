using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Features.Lookups;
using QuantumBuild.Core.Application.Features.Lookups.DTOs;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/lookups")]
[Authorize]
public class LookupsController : ControllerBase
{
    private readonly ILookupService _lookupService;
    private readonly ICurrentUserService _currentUserService;

    public LookupsController(ILookupService lookupService, ICurrentUserService currentUserService)
    {
        _lookupService = lookupService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Get effective lookup values for a category (tenant-aware)
    /// </summary>
    [HttpGet("{categoryName}/values")]
    public async Task<IActionResult> GetValues(string categoryName, [FromQuery] bool includeDisabled = false)
    {
        var tenantId = _currentUserService.TenantId;
        var result = await _lookupService.GetEffectiveValuesAsync(tenantId, categoryName, includeDisabled);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Get all lookup categories
    /// </summary>
    [HttpGet("categories")]
    [Authorize(Policy = "Core.Admin")]
    public async Task<IActionResult> GetCategories()
    {
        var result = await _lookupService.GetCategoriesAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Create a tenant custom lookup value
    /// </summary>
    [HttpPost("{categoryName}/values")]
    [Authorize(Policy = "Core.Admin")]
    public async Task<IActionResult> CreateValue(string categoryName, [FromBody] CreateTenantLookupValueDto dto)
    {
        var result = await _lookupService.CreateTenantValueAsync(categoryName, dto);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetValues), new { categoryName }, result);
    }

    /// <summary>
    /// Update a tenant lookup value
    /// </summary>
    [HttpPut("values/{id:guid}")]
    [Authorize(Policy = "Core.Admin")]
    public async Task<IActionResult> UpdateValue(Guid id, [FromBody] UpdateTenantLookupValueDto dto)
    {
        var result = await _lookupService.UpdateTenantValueAsync(id, dto);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Delete a tenant custom lookup value
    /// </summary>
    [HttpDelete("values/{id:guid}")]
    [Authorize(Policy = "Core.Admin")]
    public async Task<IActionResult> DeleteValue(Guid id)
    {
        var result = await _lookupService.DeleteTenantValueAsync(id);

        if (!result.Success)
            return BadRequest(result);

        return NoContent();
    }

    /// <summary>
    /// Toggle a global lookup value for the current tenant (enable/disable)
    /// </summary>
    [HttpPut("{categoryName}/values/{lookupValueId:guid}/toggle")]
    [Authorize(Policy = "Core.Admin")]
    public async Task<IActionResult> ToggleGlobalValue(string categoryName, Guid lookupValueId, [FromBody] ToggleGlobalValueDto dto)
    {
        var result = await _lookupService.ToggleGlobalValueAsync(categoryName, lookupValueId, dto.IsEnabled);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
