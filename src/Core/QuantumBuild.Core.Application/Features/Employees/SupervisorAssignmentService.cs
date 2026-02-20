using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Features.Employees.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.Employees;

public class SupervisorAssignmentService : ISupervisorAssignmentService
{
    private readonly ICoreDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<SupervisorAssignmentService> _logger;

    public SupervisorAssignmentService(
        ICoreDbContext context,
        ICurrentUserService currentUserService,
        UserManager<User> userManager,
        ILogger<SupervisorAssignmentService> logger)
    {
        _context = context;
        _currentUserService = currentUserService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<Result<List<SupervisorOperatorDto>>> GetAssignedOperatorsAsync(Guid supervisorEmployeeId)
    {
        try
        {
            var accessCheck = await CheckSupervisorAccessAsync(supervisorEmployeeId);
            if (!accessCheck.Success)
                return Result.Fail<List<SupervisorOperatorDto>>(accessCheck.Errors);

            var operators = await _context.SupervisorAssignments
                .Where(sa => sa.SupervisorEmployeeId == supervisorEmployeeId)
                .Select(sa => new SupervisorOperatorDto(
                    sa.Operator.Id,
                    sa.Operator.EmployeeCode,
                    sa.Operator.FirstName + " " + sa.Operator.LastName,
                    sa.Operator.Department,
                    sa.Operator.JobTitle
                ))
                .OrderBy(o => o.FullName)
                .ToListAsync();

            return Result.Ok(operators);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assigned operators for supervisor {SupervisorEmployeeId}", supervisorEmployeeId);
            return Result.Fail<List<SupervisorOperatorDto>>($"Error retrieving assigned operators: {ex.Message}");
        }
    }

    public async Task<Result<List<SupervisorOperatorDto>>> GetAvailableOperatorsAsync(Guid supervisorEmployeeId)
    {
        try
        {
            _logger.LogWarning("[AvailOps] START — supervisorEmployeeId={SupervisorEmployeeId}", supervisorEmployeeId);
            _logger.LogWarning("[AvailOps] TenantId={TenantId}, IsSuperUser={IsSuperUser}, UserId={UserId}",
                _currentUserService.TenantId, _currentUserService.IsSuperUser, _currentUserService.UserId);

            var accessCheck = await CheckSupervisorAccessAsync(supervisorEmployeeId);
            if (!accessCheck.Success)
            {
                _logger.LogWarning("[AvailOps] Access check FAILED: {Errors}", string.Join("; ", accessCheck.Errors));
                return Result.Fail<List<SupervisorOperatorDto>>(accessCheck.Errors);
            }

            // Log total employees visible through tenant filter
            var allEmployees = await _context.Employees.ToListAsync();
            _logger.LogWarning("[AvailOps] Total employees visible (all, incl inactive/deleted filtered): {Count}", allEmployees.Count);
            foreach (var emp in allEmployees)
            {
                _logger.LogWarning("[AvailOps]   Employee: Id={Id}, Name={Name}, IsActive={IsActive}, UserId={UserId}, TenantId={TenantId}",
                    emp.Id, emp.FirstName + " " + emp.LastName, emp.IsActive, emp.UserId ?? "(null)", emp.TenantId);
            }

            var activeEmployees = allEmployees.Where(e => e.IsActive).ToList();
            _logger.LogWarning("[AvailOps] Active employees: {Count}", activeEmployees.Count);

            // Get IDs of employees already assigned to this supervisor
            var assignedOperatorIds = await _context.SupervisorAssignments
                .Where(sa => sa.SupervisorEmployeeId == supervisorEmployeeId)
                .Select(sa => sa.OperatorEmployeeId)
                .ToListAsync();
            _logger.LogWarning("[AvailOps] Already assigned operator IDs: [{Ids}]",
                assignedOperatorIds.Count > 0 ? string.Join(", ", assignedOperatorIds) : "(none)");

            // Get employee IDs to exclude: those whose linked User is Admin, Supervisor, or SuperUser
            var excludedEmployeeIds = await GetExcludedEmployeeIdsAsync();
            _logger.LogWarning("[AvailOps] Excluded employee IDs (Admin/Supervisor/SuperUser): [{Ids}]",
                excludedEmployeeIds.Count > 0 ? string.Join(", ", excludedEmployeeIds) : "(none)");

            var availableOperators = await _context.Employees
                .Where(e => e.IsActive
                    && e.Id != supervisorEmployeeId
                    && !assignedOperatorIds.Contains(e.Id)
                    && !excludedEmployeeIds.Contains(e.Id))
                .Select(e => new SupervisorOperatorDto(
                    e.Id,
                    e.EmployeeCode,
                    e.FirstName + " " + e.LastName,
                    e.Department,
                    e.JobTitle
                ))
                .OrderBy(o => o.FullName)
                .ToListAsync();

            _logger.LogWarning("[AvailOps] Final available operators: {Count}", availableOperators.Count);
            foreach (var op in availableOperators)
            {
                _logger.LogWarning("[AvailOps]   Available: Id={Id}, Name={Name}", op.EmployeeId, op.FullName);
            }

            return Result.Ok(availableOperators);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available operators for supervisor {SupervisorEmployeeId}", supervisorEmployeeId);
            return Result.Fail<List<SupervisorOperatorDto>>($"Error retrieving available operators: {ex.Message}");
        }
    }

    public async Task<Result<List<SupervisorAssignmentDto>>> AssignOperatorsAsync(Guid supervisorEmployeeId, AssignOperatorsDto dto)
    {
        try
        {
            var accessCheck = await CheckSupervisorAccessAsync(supervisorEmployeeId);
            if (!accessCheck.Success)
                return Result.Fail<List<SupervisorAssignmentDto>>(accessCheck.Errors);

            // Validate supervisor exists and is active
            var supervisorExists = await _context.Employees
                .AnyAsync(e => e.Id == supervisorEmployeeId && e.IsActive);
            if (!supervisorExists)
                return Result.Fail<List<SupervisorAssignmentDto>>("Supervisor employee not found or is inactive");

            // Get employee IDs to exclude: those whose linked User is Admin, Supervisor, or SuperUser
            var excludedEmployeeIds = await GetExcludedEmployeeIdsAsync();

            // Validate all operator employees exist, are active, and are not Admin/Supervisor/SuperUser
            foreach (var operatorId in dto.OperatorEmployeeIds)
            {
                var operatorExists = await _context.Employees
                    .AnyAsync(e => e.Id == operatorId && e.IsActive);

                if (!operatorExists)
                    return Result.Fail<List<SupervisorAssignmentDto>>($"Employee with ID {operatorId} not found or is inactive");

                if (excludedEmployeeIds.Contains(operatorId))
                    return Result.Fail<List<SupervisorAssignmentDto>>($"Employee {operatorId} has an Admin, Supervisor, or SuperUser role and cannot be assigned as a team member");

                if (operatorId == supervisorEmployeeId)
                    return Result.Fail<List<SupervisorAssignmentDto>>("A supervisor cannot be assigned to themselves");
            }

            // Get existing non-deleted assignments to prevent duplicates
            var existingOperatorIds = await _context.SupervisorAssignments
                .Where(sa => sa.SupervisorEmployeeId == supervisorEmployeeId
                    && dto.OperatorEmployeeIds.Contains(sa.OperatorEmployeeId))
                .Select(sa => sa.OperatorEmployeeId)
                .ToListAsync();

            var newAssignments = new List<SupervisorAssignment>();
            foreach (var operatorId in dto.OperatorEmployeeIds)
            {
                if (existingOperatorIds.Contains(operatorId))
                    continue;

                var assignment = new SupervisorAssignment
                {
                    Id = Guid.NewGuid(),
                    SupervisorEmployeeId = supervisorEmployeeId,
                    OperatorEmployeeId = operatorId
                };

                _context.SupervisorAssignments.Add(assignment);
                newAssignments.Add(assignment);
            }

            if (newAssignments.Count == 0)
                return Result.Fail<List<SupervisorAssignmentDto>>("All specified operators are already assigned to this supervisor");

            await _context.SaveChangesAsync();

            // Reload with navigation properties to build DTOs
            var assignmentIds = newAssignments.Select(a => a.Id).ToList();
            var createdAssignments = await _context.SupervisorAssignments
                .Where(sa => assignmentIds.Contains(sa.Id))
                .Select(sa => new SupervisorAssignmentDto(
                    sa.Id,
                    sa.SupervisorEmployeeId,
                    sa.Supervisor.FirstName + " " + sa.Supervisor.LastName,
                    sa.OperatorEmployeeId,
                    sa.Operator.FirstName + " " + sa.Operator.LastName,
                    sa.CreatedAt,
                    sa.CreatedBy
                ))
                .ToListAsync();

            _logger.LogInformation(
                "Assigned {Count} operators to supervisor {SupervisorEmployeeId}",
                newAssignments.Count, supervisorEmployeeId);

            return Result.Ok(createdAssignments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning operators to supervisor {SupervisorEmployeeId}", supervisorEmployeeId);
            return Result.Fail<List<SupervisorAssignmentDto>>($"Error assigning operators: {ex.Message}");
        }
    }

    public async Task<Result> UnassignOperatorAsync(Guid supervisorEmployeeId, Guid operatorEmployeeId)
    {
        try
        {
            var accessCheck = await CheckSupervisorAccessAsync(supervisorEmployeeId);
            if (!accessCheck.Success)
                return Result.Fail(accessCheck.Errors);

            var assignment = await _context.SupervisorAssignments
                .FirstOrDefaultAsync(sa =>
                    sa.SupervisorEmployeeId == supervisorEmployeeId
                    && sa.OperatorEmployeeId == operatorEmployeeId);

            if (assignment == null)
                return Result.Fail("Assignment not found");

            assignment.IsDeleted = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Unassigned operator {OperatorEmployeeId} from supervisor {SupervisorEmployeeId}",
                operatorEmployeeId, supervisorEmployeeId);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unassigning operator {OperatorEmployeeId} from supervisor {SupervisorEmployeeId}",
                operatorEmployeeId, supervisorEmployeeId);
            return Result.Fail($"Error unassigning operator: {ex.Message}");
        }
    }

    public async Task<Result<List<Guid>>> GetAssignedOperatorIdsAsync(Guid supervisorEmployeeId)
    {
        try
        {
            var operatorIds = await _context.SupervisorAssignments
                .Where(sa => sa.SupervisorEmployeeId == supervisorEmployeeId)
                .Select(sa => sa.OperatorEmployeeId)
                .ToListAsync();

            return Result.Ok(operatorIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving assigned operator IDs for supervisor {SupervisorEmployeeId}", supervisorEmployeeId);
            return Result.Fail<List<Guid>>($"Error retrieving assigned operator IDs: {ex.Message}");
        }
    }

    private async Task<Result> CheckSupervisorAccessAsync(Guid supervisorEmployeeId)
    {
        // SuperUser or Admin can manage any supervisor's assignments
        if (_currentUserService.IsSuperUser)
            return Result.Ok();

        var currentUser = await _userManager.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id.ToString() == _currentUserService.UserId);

        if (currentUser == null)
            return Result.Fail("Current user not found");

        var isAdmin = currentUser.UserRoles.Any(ur =>
            ur.Role.NormalizedName == "ADMIN");

        if (isAdmin)
            return Result.Ok();

        // Supervisor can only manage their own assignments
        var isSupervisor = currentUser.UserRoles.Any(ur =>
            ur.Role.NormalizedName == "SUPERVISOR");

        if (isSupervisor)
        {
            // Check via User.EmployeeId first, then fall back to Employee.UserId
            if (currentUser.EmployeeId == supervisorEmployeeId)
                return Result.Ok();

            var linkedEmployee = await _context.Employees
                .AnyAsync(e => e.Id == supervisorEmployeeId && e.UserId == _currentUserService.UserId);

            if (linkedEmployee)
                return Result.Ok();
        }

        return Result.Fail("You do not have permission to manage assignments for this supervisor");
    }

    /// <summary>
    /// Returns employee IDs that should be excluded from assignment — those whose linked User
    /// has an Admin role, Supervisor role, or is a SuperUser.
    /// </summary>
    private async Task<HashSet<Guid>> GetExcludedEmployeeIdsAsync()
    {
        // Find users who are Admin, Supervisor, or SuperUser in this tenant
        var excludedUsers = await _userManager.Users
            .Where(u => u.TenantId == _currentUserService.TenantId && u.IsActive)
            .Where(u => u.IsSuperUser
                || u.UserRoles.Any(ur =>
                    ur.Role.NormalizedName == "ADMIN"
                    || ur.Role.NormalizedName == "SUPERVISOR"))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.IsSuperUser, u.EmployeeId,
                Roles = u.UserRoles.Select(ur => ur.Role.NormalizedName).ToList() })
            .ToListAsync();

        var excludedUserIds = excludedUsers.Select(u => u.Id.ToString()).ToList();

        _logger.LogWarning("[ExcludedEmp] Tenant={TenantId} — found {Count} excluded users (Admin/Supervisor/SuperUser):",
            _currentUserService.TenantId, excludedUsers.Count);
        foreach (var u in excludedUsers)
        {
            _logger.LogWarning("[ExcludedEmp]   User: Id={UserId}, Name={Name}, IsSuperUser={IsSuperUser}, EmployeeId={EmployeeId}, Roles=[{Roles}]",
                u.Id, u.FirstName + " " + u.LastName, u.IsSuperUser, u.EmployeeId?.ToString() ?? "(null)", string.Join(", ", u.Roles));
        }

        // Map those user IDs to employee IDs via Employee.UserId
        var excludedEmployeeIds = await _context.Employees
            .Where(e => e.UserId != null && excludedUserIds.Contains(e.UserId))
            .Select(e => new { e.Id, e.UserId, e.FirstName, e.LastName })
            .ToListAsync();

        _logger.LogWarning("[ExcludedEmp] Mapped to {Count} excluded employees:", excludedEmployeeIds.Count);
        foreach (var e in excludedEmployeeIds)
        {
            _logger.LogWarning("[ExcludedEmp]   Employee: Id={Id}, Name={Name}, UserId={UserId}",
                e.Id, e.FirstName + " " + e.LastName, e.UserId);
        }

        return excludedEmployeeIds.Select(e => e.Id).ToHashSet();
    }
}
