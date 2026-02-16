using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Features.Sites;
using QuantumBuild.Core.Application.Features.Sites.DTOs;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/sites")]
[Authorize]
public class SitesController : ControllerBase
{
    private readonly ISiteService _siteService;

    public SitesController(ISiteService siteService)
    {
        _siteService = siteService;
    }

    /// <summary>
    /// Get all sites (non-paginated)
    /// </summary>
    /// <returns>List of sites</returns>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _siteService.GetAllAsync();

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get sites with pagination, sorting, and search
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    /// <param name="sortColumn">Column to sort by</param>
    /// <param name="sortDirection">Sort direction (asc/desc)</param>
    /// <param name="search">Search term</param>
    /// <returns>Paginated list of sites</returns>
    [HttpGet]
    public async Task<IActionResult> GetPaginated(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null)
    {
        var query = new GetSitesQueryDto(pageNumber, pageSize, sortColumn, sortDirection, search);
        var result = await _siteService.GetPaginatedAsync(query);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get a site by ID
    /// </summary>
    /// <param name="id">Site ID</param>
    /// <returns>Site details</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _siteService.GetByIdAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new site
    /// </summary>
    /// <param name="dto">Site creation data</param>
    /// <returns>Created site</returns>
    [HttpPost]
    [Authorize(Policy = "Core.ManageSites")]
    public async Task<IActionResult> Create([FromBody] CreateSiteDto dto)
    {
        var result = await _siteService.CreateAsync(dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Update an existing site
    /// </summary>
    /// <param name="id">Site ID</param>
    /// <param name="dto">Site update data</param>
    /// <returns>Updated site</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Core.ManageSites")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSiteDto dto)
    {
        var result = await _siteService.UpdateAsync(id, dto);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete a site (soft delete)
    /// </summary>
    /// <param name="id">Site ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Core.ManageSites")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _siteService.DeleteAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return NoContent();
    }
}
