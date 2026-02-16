using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Features.Employees;
using QuantumBuild.Core.Application.Features.Employees.DTOs;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;

    public EmployeesController(IEmployeeService employeeService)
    {
        _employeeService = employeeService;
    }

    /// <summary>
    /// Get employees that do not have a linked User account
    /// </summary>
    /// <returns>List of unlinked employees</returns>
    [HttpGet("unlinked")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> GetUnlinked()
    {
        var result = await _employeeService.GetUnlinkedAsync();

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get all employees (non-paginated)
    /// </summary>
    /// <returns>List of employees</returns>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _employeeService.GetAllAsync();

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get employees with pagination, sorting, and search
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    /// <param name="sortColumn">Column to sort by</param>
    /// <param name="sortDirection">Sort direction (asc/desc)</param>
    /// <param name="search">Search term</param>
    /// <returns>Paginated list of employees</returns>
    [HttpGet]
    public async Task<IActionResult> GetPaginated(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null)
    {
        var query = new GetEmployeesQueryDto(pageNumber, pageSize, sortColumn, sortDirection, search);
        var result = await _employeeService.GetPaginatedAsync(query);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get an employee by ID
    /// </summary>
    /// <param name="id">Employee ID</param>
    /// <returns>Employee details</returns>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _employeeService.GetByIdAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new employee
    /// </summary>
    /// <param name="dto">Employee creation data</param>
    /// <returns>Created employee</returns>
    [HttpPost]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDto dto)
    {
        var result = await _employeeService.CreateAsync(dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Update an existing employee
    /// </summary>
    /// <param name="id">Employee ID</param>
    /// <param name="dto">Employee update data</param>
    /// <returns>Updated employee</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeDto dto)
    {
        var result = await _employeeService.UpdateAsync(id, dto);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete an employee (soft delete)
    /// </summary>
    /// <param name="id">Employee ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _employeeService.DeleteAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return NoContent();
    }

    /// <summary>
    /// Resend the welcome/password setup email to an employee with a linked user account
    /// </summary>
    /// <param name="id">Employee ID</param>
    /// <returns>Success message or error</returns>
    [HttpPost("{id:guid}/resend-invite")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> ResendInvite(Guid id)
    {
        var result = await _employeeService.ResendInviteAsync(id);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Link an existing employee to an existing user account
    /// </summary>
    /// <param name="id">Employee ID</param>
    /// <param name="dto">User ID to link</param>
    /// <returns>Updated employee</returns>
    [HttpPost("{id:guid}/link-user")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> LinkToUser(Guid id, [FromBody] LinkEmployeeToUserDto dto)
    {
        var result = await _employeeService.LinkToUserAsync(id, dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new user account for an existing employee
    /// </summary>
    /// <param name="id">Employee ID</param>
    /// <param name="dto">Role IDs for the new user</param>
    /// <returns>Updated employee</returns>
    [HttpPost("{id:guid}/create-user")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> CreateUserForEmployee(Guid id, [FromBody] CreateUserForEmployeeDto dto)
    {
        var result = await _employeeService.CreateUserForEmployeeAsync(id, dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = id }, result);
    }

    /// <summary>
    /// Unlink the user account from an employee (does not delete the user)
    /// </summary>
    /// <param name="id">Employee ID</param>
    /// <returns>Success or error</returns>
    [HttpDelete("{id:guid}/unlink-user")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> UnlinkUser(Guid id)
    {
        var result = await _employeeService.UnlinkUserAsync(id);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
