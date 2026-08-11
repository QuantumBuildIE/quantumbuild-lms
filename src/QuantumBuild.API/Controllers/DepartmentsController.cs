using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Features.Departments;
using QuantumBuild.Core.Application.Features.Departments.DTOs;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController : ControllerBase
{
    private readonly IDepartmentService _departmentService;

    public DepartmentsController(IDepartmentService departmentService)
    {
        _departmentService = departmentService;
    }

    /// <summary>
    /// Get all departments (non-paginated)
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _departmentService.GetAllAsync();

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get departments with pagination, sorting, and search
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPaginated(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null)
    {
        var query = new GetDepartmentsQueryDto(pageNumber, pageSize, sortColumn, sortDirection, search);
        var result = await _departmentService.GetPaginatedAsync(query);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get a department by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _departmentService.GetByIdAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new department
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Core.ManageDepartments")]
    public async Task<IActionResult> Create([FromBody] CreateDepartmentDto dto)
    {
        var result = await _departmentService.CreateAsync(dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Update an existing department. Deactivation is done via this endpoint by
    /// setting IsActive to false. There is no separate delete endpoint, since
    /// employees may already reference the department (nullable FK, SetNull on
    /// true removal is intentionally not exposed here).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Core.ManageDepartments")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDepartmentDto dto)
    {
        var result = await _departmentService.UpdateAsync(id, dto);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }
}
