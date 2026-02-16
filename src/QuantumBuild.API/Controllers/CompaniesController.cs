using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Features.Companies;
using QuantumBuild.Core.Application.Features.Companies.DTOs;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    /// <summary>
    /// Get all companies (non-paginated)
    /// </summary>
    /// <returns>List of companies</returns>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _companyService.GetAllAsync();

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get companies with pagination, sorting, and search
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    /// <param name="sortColumn">Column to sort by</param>
    /// <param name="sortDirection">Sort direction (asc/desc)</param>
    /// <param name="search">Search term</param>
    /// <param name="companyType">Filter by company type</param>
    /// <returns>Paginated list of companies</returns>
    [HttpGet]
    public async Task<IActionResult> GetPaginated(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null,
        [FromQuery] string? companyType = null)
    {
        var query = new GetCompaniesQueryDto(pageNumber, pageSize, sortColumn, sortDirection, search, companyType);
        var result = await _companyService.GetPaginatedAsync(query);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get a company by ID
    /// </summary>
    /// <param name="id">Company ID</param>
    /// <returns>Company details with contacts</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _companyService.GetByIdAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new company
    /// </summary>
    /// <param name="dto">Company creation data</param>
    /// <returns>Created company</returns>
    [HttpPost]
    [Authorize(Policy = "Core.ManageCompanies")]
    public async Task<IActionResult> Create([FromBody] CreateCompanyDto dto)
    {
        var result = await _companyService.CreateAsync(dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Update an existing company
    /// </summary>
    /// <param name="id">Company ID</param>
    /// <param name="dto">Company update data</param>
    /// <returns>Updated company</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Core.ManageCompanies")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyDto dto)
    {
        var result = await _companyService.UpdateAsync(id, dto);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete a company (soft delete)
    /// </summary>
    /// <param name="id">Company ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Core.ManageCompanies")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _companyService.DeleteAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return NoContent();
    }
}
