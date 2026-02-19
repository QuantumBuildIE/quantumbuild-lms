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
            var accessCheck = await CheckSupervisorAccessAsync(supervisorEmployeeId);
            if (!accessCheck.Success)
                return Result.Fail<List<SupervisorOperatorDto>>(accessCheck.Errors);

            // Get IDs of operators already assigned to this supervisor
            var assignedOperatorIds = await _context.SupervisorAssignments
                .Where(sa => sa.SupervisorEmployeeId == supervisorEmployeeId)
                .Select(sa => sa.OperatorEmployeeId)
                .ToListAsync();

            // Find employees who have the Operator role and are not yet assigned
            var operatorRoleId = await _userManager.Users
                .Where(u => u.TenantId == _currentUserService.TenantId)
                .SelectMany(u => u.UserRoles)
                .Where(ur => ur.Role.NormalizedName == "OPERATOR")
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync();

            // Get employee IDs for users with Operator role
            var operatorEmployeeIds = await _userManager.Users
                .Where(u => operatorRoleId.Contains(u.Id) && u.EmployeeId.HasValue)
                .Select(u => u.EmployeeId!.Value)
                .ToListAsync();

            var availableOperators = await _context.Employees
                .Where(e => operatorEmployeeIds.Contains(e.Id)
                    && !assignedOperatorIds.Contains(e.Id)
                    && e.IsActive
                    && e.Id != supervisorEmployeeId)
                .Select(e => new SupervisorOperatorDto(
                    e.Id,
                    e.EmployeeCode,
                    e.FirstName + " " + e.LastName,
                    e.Department,
                    e.JobTitle
                ))
                .OrderBy(o => o.FullName)
                .ToListAsync();

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

            // Validate supervisor has the Supervisor role
            var supervisorRoleCheck = await ValidateEmployeeHasRoleAsync(supervisorEmployeeId, "Supervisor");
            if (!supervisorRoleCheck.Success)
                return Result.Fail<List<SupervisorAssignmentDto>>($"Employee {supervisorEmployeeId} does not have the Supervisor role");

            // Validate all operator employees exist and have the Operator role
            foreach (var operatorId in dto.OperatorEmployeeIds)
            {
                var operatorExists = await _context.Employees
                    .AnyAsync(e => e.Id == operatorId && e.IsActive);

                if (!operatorExists)
                    return Result.Fail<List<SupervisorAssignmentDto>>($"Operator employee with ID {operatorId} not found or is inactive");

                var operatorRoleCheck = await ValidateEmployeeHasRoleAsync(operatorId, "Operator");
                if (!operatorRoleCheck.Success)
                    return Result.Fail<List<SupervisorAssignmentDto>>($"Employee {operatorId} does not have the Operator role");
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

        if (isSupervisor && currentUser.EmployeeId == supervisorEmployeeId)
            return Result.Ok();

        return Result.Fail("You do not have permission to manage assignments for this supervisor");
    }

    private async Task<Result> ValidateEmployeeHasRoleAsync(Guid employeeId, string roleName)
    {
        var hasRole = await _userManager.Users
            .Where(u => u.EmployeeId == employeeId)
            .SelectMany(u => u.UserRoles)
            .AnyAsync(ur => ur.Role.NormalizedName == roleName.ToUpperInvariant());

        return hasRole ? Result.Ok() : Result.Fail($"Employee does not have the {roleName} role");
    }
}
