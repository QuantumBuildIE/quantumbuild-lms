using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Features.Users;
using QuantumBuild.Core.Application.Features.Users.DTOs;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Get all users (non-paginated)
    /// </summary>
    /// <returns>List of users</returns>
    [HttpGet("all")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _userService.GetAllAsync();

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get users with pagination, sorting, and search
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20)</param>
    /// <param name="sortColumn">Column to sort by</param>
    /// <param name="sortDirection">Sort direction (asc/desc)</param>
    /// <param name="search">Search term</param>
    /// <returns>Paginated list of users</returns>
    [HttpGet]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> GetPaginated(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? search = null)
    {
        var query = new GetUsersQueryDto(pageNumber, pageSize, sortColumn, sortDirection, search);
        var result = await _userService.GetPaginatedAsync(query);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get a user by ID
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>User details</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _userService.GetByIdAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    /// <param name="dto">User creation data</param>
    /// <returns>Created user</returns>
    [HttpPost]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        var result = await _userService.CreateAsync(dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Update an existing user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="dto">User update data</param>
    /// <returns>Updated user</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        var result = await _userService.UpdateAsync(id, dto);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Delete a user (soft delete / deactivate)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _userService.DeleteAsync(id);

        if (!result.Success)
        {
            return NotFound(result);
        }

        return NoContent();
    }

    /// <summary>
    /// Admin reset password for a user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="dto">New password data</param>
    /// <returns>Success result</returns>
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordDto dto)
    {
        var result = await _userService.ResetPasswordAsync(id, dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// User changes their own password
    /// </summary>
    /// <param name="dto">Password change data</param>
    /// <returns>Success result</returns>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return BadRequest(new { error = "Invalid user." });
        }

        var result = await _userService.ChangePasswordAsync(userId, dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get users that do not have a linked employee record
    /// </summary>
    /// <returns>List of unlinked users</returns>
    [HttpGet("unlinked")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> GetUnlinked()
    {
        var result = await _userService.GetUnlinkedAsync();

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Link an existing user to an existing employee record
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="dto">Employee ID to link</param>
    /// <returns>Updated user</returns>
    [HttpPost("{id:guid}/link-employee")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> LinkToEmployee(Guid id, [FromBody] LinkUserToEmployeeDto dto)
    {
        var result = await _userService.LinkToEmployeeAsync(id, dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Create a new employee record for an existing user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="dto">Employee details</param>
    /// <returns>Updated user</returns>
    [HttpPost("{id:guid}/create-employee")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> CreateEmployeeForUser(Guid id, [FromBody] CreateEmployeeForUserDto dto)
    {
        var result = await _userService.CreateEmployeeForUserAsync(id, dto);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = id }, result);
    }

    /// <summary>
    /// Unlink the employee record from a user (does not delete the employee)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>Success or error</returns>
    [HttpDelete("{id:guid}/unlink-employee")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> UnlinkEmployee(Guid id)
    {
        var result = await _userService.UnlinkEmployeeAsync(id);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}
