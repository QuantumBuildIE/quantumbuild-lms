using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Constants;
using QuantumBuild.Core.Application.Features.Employees;
using QuantumBuild.Core.Application.Features.Employees.DTOs;
using QuantumBuild.Core.Application.Interfaces;

namespace QuantumBuild.API.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly ISupervisorAssignmentService _supervisorAssignmentService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISystemAuditLogger _auditLogger;

    public EmployeesController(
        IEmployeeService employeeService,
        ISupervisorAssignmentService supervisorAssignmentService,
        ICurrentUserService currentUserService,
        ISystemAuditLogger auditLogger)
    {
        _employeeService = employeeService;
        _supervisorAssignmentService = supervisorAssignmentService;
        _currentUserService = currentUserService;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// Get employees that do not have a linked User account
    /// </summary>
    [HttpGet("unlinked")]
    [Authorize(Policy = "Core.ManageUsers")]
    public async Task<IActionResult> GetUnlinked()
    {
        var result = await _employeeService.GetUnlinkedAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get all employees (non-paginated)
    /// </summary>
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var result = await _employeeService.GetAllAsync();

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get employees with pagination, sorting, and search
    /// </summary>
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
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get an employee by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _employeeService.GetByIdAsync(id);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    /// <summary>
    /// Create a new employee
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> Create([FromBody] CreateEmployeeDto dto)
    {
        var result = await _employeeService.CreateAsync(dto);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.Employee.Create, success: false,
                entityType: "Employee", failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return BadRequest(result);
        }

        await _auditLogger.LogAsync(AuditActions.Employee.Create, success: true,
            entityType: "Employee", entityId: result.Data!.Id, entityDisplayName: result.Data!.FullName);
        return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result);
    }

    /// <summary>
    /// Update an existing employee
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEmployeeDto dto)
    {
        var result = await _employeeService.UpdateAsync(id, dto);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.Employee.Update, success: false,
                entityType: "Employee", entityId: id,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return NotFound(result);
        }

        await _auditLogger.LogAsync(AuditActions.Employee.Update, success: true,
            entityType: "Employee", entityId: id, entityDisplayName: result.Data!.FullName);
        return Ok(result);
    }

    /// <summary>
    /// Delete an employee (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _employeeService.GetByIdAsync(id);
        var result = await _employeeService.DeleteAsync(id);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.Employee.Delete, success: false,
                entityType: "Employee", entityId: id,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return NotFound(result);
        }

        await _auditLogger.LogAsync(AuditActions.Employee.Delete, success: true,
            entityType: "Employee", entityId: id, entityDisplayName: existing.Data?.FullName);
        return NoContent();
    }

    /// <summary>
    /// Resend the welcome/password setup email to an employee with a linked user account
    /// </summary>
    [HttpPost("{id:guid}/resend-invite")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> ResendInvite(Guid id)
    {
        var result = await _employeeService.ResendInviteAsync(id);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Link an existing employee to an existing user account
    /// </summary>
    [HttpPost("{id:guid}/link-user")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> LinkToUser(Guid id, [FromBody] LinkEmployeeToUserDto dto)
    {
        var result = await _employeeService.LinkToUserAsync(id, dto);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Create a new user account for an existing employee
    /// </summary>
    [HttpPost("{id:guid}/create-user")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> CreateUserForEmployee(Guid id, [FromBody] CreateUserForEmployeeDto dto)
    {
        var result = await _employeeService.CreateUserForEmployeeAsync(id, dto);

        if (!result.Success)
            return BadRequest(result);

        return CreatedAtAction(nameof(GetById), new { id = id }, result);
    }

    /// <summary>
    /// Unlink the user account from an employee (does not delete the user)
    /// </summary>
    [HttpDelete("{id:guid}/unlink-user")]
    [Authorize(Policy = "Core.ManageEmployees")]
    public async Task<IActionResult> UnlinkUser(Guid id)
    {
        var result = await _employeeService.UnlinkUserAsync(id);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    // ── Supervisor Assignment Endpoints ──────────────────────────────────

    /// <summary>
    /// Get operators assigned to a supervisor
    /// </summary>
    [HttpGet("{supervisorId:guid}/operators")]
    [Authorize(Policy = "Learnings.View")]
    public async Task<IActionResult> GetAssignedOperators(Guid supervisorId)
    {
        if (!CanAccessSupervisorData(supervisorId))
            return Forbid();

        var result = await _supervisorAssignmentService.GetAssignedOperatorsAsync(supervisorId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Get operators available to assign to a supervisor
    /// </summary>
    [HttpGet("{supervisorId:guid}/operators/available")]
    [Authorize(Policy = "Learnings.View")]
    public async Task<IActionResult> GetAvailableOperators(Guid supervisorId)
    {
        if (!CanManageSupervisorData(supervisorId))
            return Forbid();

        var result = await _supervisorAssignmentService.GetAvailableOperatorsAsync(supervisorId);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// Assign operators to a supervisor
    /// </summary>
    [HttpPost("{supervisorId:guid}/operators")]
    [Authorize(Policy = "Learnings.View")]
    public async Task<IActionResult> AssignOperators(Guid supervisorId, [FromBody] AssignOperatorsDto dto)
    {
        if (!CanManageSupervisorData(supervisorId))
            return Forbid();

        var result = await _supervisorAssignmentService.AssignOperatorsAsync(supervisorId, dto);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.Employee.AssignOperator, success: false,
                entityType: "Employee", entityId: supervisorId,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return BadRequest(result);
        }

        await _auditLogger.LogAsync(AuditActions.Employee.AssignOperator, success: true,
            entityType: "Employee", entityId: supervisorId,
            metadataJson: JsonSerializer.Serialize(new { operatorIds = dto.OperatorEmployeeIds }));
        return Ok(result);
    }

    /// <summary>
    /// Unassign an operator from a supervisor
    /// </summary>
    [HttpDelete("{supervisorId:guid}/operators/{operatorId:guid}")]
    [Authorize(Policy = "Learnings.View")]
    public async Task<IActionResult> UnassignOperator(Guid supervisorId, Guid operatorId)
    {
        if (!CanManageSupervisorData(supervisorId))
            return Forbid();

        var result = await _supervisorAssignmentService.UnassignOperatorAsync(supervisorId, operatorId);

        if (!result.Success)
        {
            await _auditLogger.LogAsync(AuditActions.Employee.UnassignOperator, success: false,
                entityType: "Employee", entityId: supervisorId,
                failureReason: result.Errors.FirstOrDefault() ?? result.Message);
            return BadRequest(result);
        }

        await _auditLogger.LogAsync(AuditActions.Employee.UnassignOperator, success: true,
            entityType: "Employee", entityId: supervisorId,
            metadataJson: JsonSerializer.Serialize(new { operatorId }));
        return Ok(result);
    }

    /// <summary>
    /// Get the current supervisor's own assigned operators
    /// </summary>
    [HttpGet("my-operators")]
    [Authorize(Policy = "Learnings.View")]
    public async Task<IActionResult> GetMyOperators()
    {
        var employeeId = GetCurrentEmployeeId();
        if (employeeId == null)
            return BadRequest(new { message = "Current user is not linked to an employee" });

        var result = await _supervisorAssignmentService.GetAssignedOperatorsAsync(employeeId.Value);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private Guid? GetCurrentEmployeeId()
    {
        var employeeIdClaim = User.FindFirst("employee_id")?.Value;
        if (string.IsNullOrEmpty(employeeIdClaim) || !Guid.TryParse(employeeIdClaim, out var employeeId))
            return null;
        return employeeId;
    }

    private bool IsAdminOrSuperUser()
    {
        var isSuperUser = User.FindAll("is_super_user")
            .Any(c => string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
        if (isSuperUser) return true;

        var hasManageEmployees = User.FindAll("permission")
            .Any(c => c.Value == "Core.ManageEmployees");
        return hasManageEmployees;
    }

    /// <summary>
    /// Supervisors can view their own data; admins/SU can view anyone's.
    /// </summary>
    private bool CanAccessSupervisorData(Guid supervisorId)
    {
        if (IsAdminOrSuperUser()) return true;
        var employeeId = GetCurrentEmployeeId();
        return employeeId.HasValue && employeeId.Value == supervisorId;
    }

    /// <summary>
    /// Admins/SU can manage anyone's; supervisors can manage their own.
    /// </summary>
    private bool CanManageSupervisorData(Guid supervisorId)
    {
        if (IsAdminOrSuperUser()) return true;
        var employeeId = GetCurrentEmployeeId();
        return employeeId.HasValue && employeeId.Value == supervisorId;
    }
}
