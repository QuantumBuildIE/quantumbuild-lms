using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Constants;
using QuantumBuild.Core.Application.Features.Users;
using QuantumBuild.Core.Application.Features.Users.DTOs;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ISystemAuditLogger _auditLogger;

    public UsersController(IUserService userService, ISystemAuditLogger auditLogger)
    {
        _userService = userService;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Get all users (non-paginated)
    /// </summary>
    [HttpGet("all")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _userService.GetAllAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get users with pagination, sorting, and search
    /// </summary>
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
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get a user by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _userService.GetByIdAsync(id);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Create a new user
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        var result = await _userService.CreateAsync(dto);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.User.Create, success: false,
                entityType: "User", failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return BadRequest(result);
        }

        await _auditLogger.LogAsync(AuditActions.User.Create, success: true,
            entityType: "User", entityId: result.Data!.Id, entityDisplayName: result.Data!.Email);
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Update an existing user
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto)
    {
        var result = await _userService.UpdateAsync(id, dto);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.User.Update, success: false,
                entityType: "User", entityId: id,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return NotFound(result);
        }

        await _auditLogger.LogAsync(AuditActions.User.Update, success: true,
            entityType: "User", entityId: id, entityDisplayName: result.Data!.Email);
        return Ok(result);
    }

    /// <summary>
    /// Delete a user (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _userService.GetByIdAsync(id);
        var result = await _userService.DeleteAsync(id);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.User.Delete, success: false,
                entityType: "User", entityId: id,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return NotFound(result);
        }

        await _auditLogger.LogAsync(AuditActions.User.Delete, success: true,
            entityType: "User", entityId: id, entityDisplayName: existing.Data?.Email);
        return NoContent();
    }

    /// <summary>
    /// Admin reset password for a user
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordDto dto)
    {
        var result = await _userService.ResetPasswordAsync(id, dto);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.User.PasswordReset, success: false,
                entityType: "User", entityId: id,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return BadRequest(result);
        }

        await _auditLogger.LogAsync(AuditActions.User.PasswordReset, success: true,
            entityType: "User", entityId: id);
        return Ok(result);
    }

    /// <summary>
    /// User changes their own password
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            return BadRequest(new { error = "Invalid user." });

        var result = await _userService.ChangePasswordAsync(userId, dto);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get users that do not have a linked employee record
    /// </summary>
    [HttpGet("unlinked")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> GetUnlinked()
    {
        var result = await _userService.GetUnlinkedAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Link an existing user to an existing employee record
    /// </summary>
    [HttpPost("{id:guid}/link-employee")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> LinkToEmployee(Guid id, [FromBody] LinkUserToEmployeeDto dto)
    {
        var result = await _userService.LinkToEmployeeAsync(id, dto);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.User.LinkEmployee, success: false,
                entityType: "User", entityId: id,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return BadRequest(result);
        }

        await _auditLogger.LogAsync(AuditActions.User.LinkEmployee, success: true,
            entityType: "User", entityId: id,
            metadataJson: JsonSerializer.Serialize(new { employeeId = dto.EmployeeId }));
        return Ok(result);
    }

    /// <summary>
    /// Create a new employee record for an existing user
    /// </summary>
    [HttpPost("{id:guid}/create-employee")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> CreateEmployeeForUser(Guid id, [FromBody] CreateEmployeeForUserDto dto)
    {
        var result = await _userService.CreateEmployeeForUserAsync(id, dto);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetById), new { id = id }, result);
    }

    /// <summary>
    /// Unlink the employee record from a user (does not delete the employee)
    /// </summary>
    [HttpDelete("{id:guid}/unlink-employee")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> UnlinkEmployee(Guid id)
    {
        var result = await _userService.UnlinkEmployeeAsync(id);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.User.UnlinkEmployee, success: false,
                entityType: "User", entityId: id,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return BadRequest(result);
        }

        await _auditLogger.LogAsync(AuditActions.User.UnlinkEmployee, success: true,
            entityType: "User", entityId: id);
        return Ok(result);
    }
}
