using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.Employees;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkSchedule;

public class UpdateToolboxTalkScheduleCommandHandler : IRequestHandler<UpdateToolboxTalkScheduleCommand, ToolboxTalkScheduleDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISupervisorAssignmentService _supervisorAssignmentService;

    public UpdateToolboxTalkScheduleCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext,
        ICurrentUserService currentUserService,
        ISupervisorAssignmentService supervisorAssignmentService)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
        _currentUserService = currentUserService;
        _supervisorAssignmentService = supervisorAssignmentService;
    }

    public async Task<ToolboxTalkScheduleDto> Handle(UpdateToolboxTalkScheduleCommand request, CancellationToken cancellationToken)
    {
        // Get the schedule with assignments
        var schedule = await _dbContext.ToolboxTalkSchedules
            .Include(s => s.Assignments)
            .Include(s => s.ToolboxTalk)
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.TenantId == request.TenantId, cancellationToken);

        if (schedule == null)
        {
            throw new InvalidOperationException($"Schedule with ID '{request.Id}' not found.");
        }

        // Only allow updates when status is Draft or Active (not yet processed to Completed/Cancelled)
        if (schedule.Status != ToolboxTalkScheduleStatus.Draft && schedule.Status != ToolboxTalkScheduleStatus.Active)
        {
            throw new InvalidOperationException($"Schedule can only be updated when in Draft or Active status. Current status: {schedule.Status}");
        }

        // Supervisor-scoping: restrict to assigned operators only
        var isSupervisorOnly = !_currentUserService.IsSuperUser
            && !_currentUserService.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            && _currentUserService.Roles.Contains("Supervisor", StringComparer.OrdinalIgnoreCase);

        List<Guid>? supervisorPermittedIds = null;
        if (isSupervisorOnly)
        {
            var supervisorEmployeeId = _currentUserService.EmployeeId;
            if (!supervisorEmployeeId.HasValue)
                throw new InvalidOperationException(
                    "Supervisor account is not linked to an employee record.");

            if (request.AssignToAllEmployees)
                throw new InvalidOperationException(
                    "Supervisors cannot schedule training for all employees. Please select your assigned team members.");

            var assignedResult = await _supervisorAssignmentService.GetAssignedOperatorIdsAsync(
                supervisorEmployeeId.Value);
            if (!assignedResult.Success)
                throw new InvalidOperationException("Could not verify supervisor assignments.");

            supervisorPermittedIds = assignedResult.Data!;

            var unauthorised = request.EmployeeIds.Except(supervisorPermittedIds).ToList();
            if (unauthorised.Any())
                throw new InvalidOperationException(
                    $"{unauthorised.Count} selected employee(s) are not in your assigned team and cannot be scheduled.");
        }

        // Get employee IDs to assign
        List<Guid> employeeIdsToAssign;
        var criteriaDerivedIds = new HashSet<Guid>();
        if (request.AssignToAllEmployees)
        {
            // Get all active employees for the tenant
            employeeIdsToAssign = await _coreDbContext.Employees
                .Where(e => e.TenantId == request.TenantId && e.IsActive && !e.IsDeleted)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);

            if (!employeeIdsToAssign.Any())
            {
                throw new InvalidOperationException("No active employees found to assign the learning.");
            }
        }
        else
        {
            // Validate provided employee IDs exist and are active
            var validEmployeeIds = await _coreDbContext.Employees
                .Where(e => e.TenantId == request.TenantId && e.IsActive && !e.IsDeleted && request.EmployeeIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);

            var invalidIds = request.EmployeeIds.Except(validEmployeeIds).ToList();
            if (invalidIds.Any())
            {
                throw new InvalidOperationException($"The following employee IDs are invalid or inactive: {string.Join(", ", invalidIds)}");
            }

            // Validate department/site targets belong to the caller's tenant
            if (request.TargetDepartmentIds.Any())
            {
                var validDepartmentIds = await _coreDbContext.Departments
                    .Where(d => d.TenantId == request.TenantId && !d.IsDeleted && request.TargetDepartmentIds.Contains(d.Id))
                    .Select(d => d.Id)
                    .ToListAsync(cancellationToken);

                var invalidDepartmentIds = request.TargetDepartmentIds.Except(validDepartmentIds).ToList();
                if (invalidDepartmentIds.Any())
                {
                    throw new InvalidOperationException($"The following department IDs are invalid: {string.Join(", ", invalidDepartmentIds)}");
                }
            }

            if (request.TargetSiteIds.Any())
            {
                var validSiteIds = await _coreDbContext.Sites
                    .Where(s => s.TenantId == request.TenantId && !s.IsDeleted && request.TargetSiteIds.Contains(s.Id))
                    .Select(s => s.Id)
                    .ToListAsync(cancellationToken);

                var invalidSiteIds = request.TargetSiteIds.Except(validSiteIds).ToList();
                if (invalidSiteIds.Any())
                {
                    throw new InvalidOperationException($"The following site IDs are invalid: {string.Join(", ", invalidSiteIds)}");
                }
            }

            // Expand department/site targets to member employees (union, active only)
            if (request.TargetDepartmentIds.Any() || request.TargetSiteIds.Any())
            {
                var expandedIds = await _coreDbContext.Employees
                    .Where(e => e.TenantId == request.TenantId && e.IsActive && !e.IsDeleted
                        && ((e.DepartmentId.HasValue && request.TargetDepartmentIds.Contains(e.DepartmentId.Value))
                            || (e.PrimarySiteId.HasValue && request.TargetSiteIds.Contains(e.PrimarySiteId.Value))))
                    .Select(e => e.Id)
                    .ToListAsync(cancellationToken);

                // Supervisors only reach the operators assigned to them, even via a department/location target
                if (isSupervisorOnly)
                {
                    expandedIds = expandedIds.Intersect(supervisorPermittedIds!).ToList();
                }

                criteriaDerivedIds = expandedIds.ToHashSet();
            }

            employeeIdsToAssign = validEmployeeIds.Union(criteriaDerivedIds).ToList();

            if (!employeeIdsToAssign.Any())
            {
                throw new InvalidOperationException("No employees resolved from the selected employees, departments, or locations.");
            }
        }

        // Update schedule properties
        // ScheduledDate/EndDate arrive as Kind=Unspecified when a client sends a date-only
        // string (e.g. "2026-07-16") with no offset — Npgsql rejects Unspecified DateTimes
        // against the timestamptz columns. Normalise to Utc before persisting.
        var scheduledDate = DateTimeUtils.EnsureUtc(request.ScheduledDate);
        var endDate = request.EndDate.HasValue ? DateTimeUtils.EnsureUtc(request.EndDate.Value) : (DateTime?)null;

        schedule.ScheduledDate = scheduledDate;
        schedule.EndDate = endDate;
        schedule.Frequency = request.Frequency;
        schedule.AssignToAllEmployees = request.AssignToAllEmployees;
        schedule.TargetDepartmentIds = request.AssignToAllEmployees ? new List<Guid>() : request.TargetDepartmentIds;
        schedule.TargetSiteIds = request.AssignToAllEmployees ? new List<Guid>() : request.TargetSiteIds;
        schedule.NextRunDate = scheduledDate;
        schedule.Notes = request.Notes;

        // Handle assignment changes
        var currentEmployeeIds = schedule.Assignments.Select(a => a.EmployeeId).ToHashSet();
        var newEmployeeIds = employeeIdsToAssign.ToHashSet();

        // Remove assignments for employees no longer in the list
        var assignmentsToRemove = schedule.Assignments
            .Where(a => !newEmployeeIds.Contains(a.EmployeeId))
            .ToList();

        // Physical delete via ExecuteDeleteAsync — DbSet.Remove() would be soft-deleted by the
        // SetAuditFields interceptor. Required-FK nav-collection Remove() also marks the entity
        // as Deleted via EF orphan-removal semantics, so we Detach explicitly to suppress the
        // phantom soft-delete UPDATE that would otherwise hit zero rows.
        var assignmentIdsToRemove = new List<Guid>();
        foreach (var assignment in assignmentsToRemove)
        {
            schedule.Assignments.Remove(assignment); // load-bearing for nav-collection consumers
            _dbContext.Entry(assignment).State = EntityState.Detached;
            assignmentIdsToRemove.Add(assignment.Id);
        }

        if (assignmentIdsToRemove.Count > 0)
        {
            await _dbContext.ToolboxTalkScheduleAssignments
                .Where(a => assignmentIdsToRemove.Contains(a.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        // Add assignments for new employees
        var employeesToAdd = newEmployeeIds.Except(currentEmployeeIds);
        foreach (var employeeId in employeesToAdd)
        {
            var assignment = new ToolboxTalkScheduleAssignment
            {
                Id = Guid.NewGuid(),
                ScheduleId = schedule.Id,
                EmployeeId = employeeId,
                IsProcessed = false,
                ProcessedAt = null,
                IsCriteriaDerived = criteriaDerivedIds.Contains(employeeId)
            };
            // schedule is an already-tracked query result, so DetectChanges resolves a
            // key-preassigned child linked via nav-collection Add() to Modified, not Added
            // (EF's Added/Modified heuristic keys off the PK's default-ness). Force Added
            // explicitly so EF issues an INSERT instead of a 0-row UPDATE.
            schedule.Assignments.Add(assignment);
            _dbContext.Entry(assignment).State = EntityState.Added;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Return the updated schedule as DTO
        return MapToDto(schedule);
    }

    private static ToolboxTalkScheduleDto MapToDto(ToolboxTalkSchedule schedule)
    {
        return new ToolboxTalkScheduleDto
        {
            Id = schedule.Id,
            ToolboxTalkId = schedule.ToolboxTalkId,
            ToolboxTalkTitle = schedule.ToolboxTalk?.Title ?? string.Empty,
            ScheduledDate = schedule.ScheduledDate,
            EndDate = schedule.EndDate,
            Frequency = schedule.Frequency,
            FrequencyDisplay = GetFrequencyDisplay(schedule.Frequency),
            AssignToAllEmployees = schedule.AssignToAllEmployees,
            TargetDepartmentIds = schedule.TargetDepartmentIds,
            TargetSiteIds = schedule.TargetSiteIds,
            Status = schedule.Status,
            StatusDisplay = GetStatusDisplay(schedule.Status),
            NextRunDate = schedule.NextRunDate,
            Notes = schedule.Notes,
            AssignmentCount = schedule.Assignments.Count,
            ProcessedCount = schedule.Assignments.Count(a => a.IsProcessed),
            Assignments = schedule.Assignments.Select(a => new ToolboxTalkScheduleAssignmentDto
            {
                Id = a.Id,
                ScheduleId = a.ScheduleId,
                EmployeeId = a.EmployeeId,
                EmployeeName = string.Empty,
                IsProcessed = a.IsProcessed,
                ProcessedAt = a.ProcessedAt
            }).ToList(),
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };
    }

    private static string GetFrequencyDisplay(ToolboxTalkFrequency frequency) => frequency switch
    {
        ToolboxTalkFrequency.Once => "Once",
        ToolboxTalkFrequency.Weekly => "Weekly",
        ToolboxTalkFrequency.Monthly => "Monthly",
        ToolboxTalkFrequency.Annually => "Annually",
        _ => frequency.ToString()
    };

    private static string GetStatusDisplay(ToolboxTalkScheduleStatus status) => status switch
    {
        ToolboxTalkScheduleStatus.Draft => "Draft",
        ToolboxTalkScheduleStatus.Active => "Active",
        ToolboxTalkScheduleStatus.Completed => "Completed",
        ToolboxTalkScheduleStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };
}
