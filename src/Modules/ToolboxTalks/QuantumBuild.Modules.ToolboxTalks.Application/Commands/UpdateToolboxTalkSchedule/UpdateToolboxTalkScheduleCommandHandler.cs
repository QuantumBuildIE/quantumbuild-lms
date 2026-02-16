using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkSchedule;

public class UpdateToolboxTalkScheduleCommandHandler : IRequestHandler<UpdateToolboxTalkScheduleCommand, ToolboxTalkScheduleDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;

    public UpdateToolboxTalkScheduleCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
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

        // Only allow updates when status is Draft
        if (schedule.Status != ToolboxTalkScheduleStatus.Draft)
        {
            throw new InvalidOperationException($"Schedule can only be updated when in Draft status. Current status: {schedule.Status}");
        }

        // Get employee IDs to assign
        List<Guid> employeeIdsToAssign;
        if (request.AssignToAllEmployees)
        {
            // Get all active employees for the tenant
            employeeIdsToAssign = await _coreDbContext.Employees
                .Where(e => e.TenantId == request.TenantId && e.IsActive && !e.IsDeleted)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);

            if (!employeeIdsToAssign.Any())
            {
                throw new InvalidOperationException("No active employees found to assign the toolbox talk.");
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

            employeeIdsToAssign = validEmployeeIds;
        }

        // Update schedule properties
        schedule.ScheduledDate = request.ScheduledDate;
        schedule.EndDate = request.EndDate;
        schedule.Frequency = request.Frequency;
        schedule.AssignToAllEmployees = request.AssignToAllEmployees;
        schedule.NextRunDate = request.ScheduledDate;
        schedule.Notes = request.Notes;

        // Handle assignment changes
        var currentEmployeeIds = schedule.Assignments.Select(a => a.EmployeeId).ToHashSet();
        var newEmployeeIds = employeeIdsToAssign.ToHashSet();

        // Remove assignments for employees no longer in the list
        var assignmentsToRemove = schedule.Assignments
            .Where(a => !newEmployeeIds.Contains(a.EmployeeId))
            .ToList();

        foreach (var assignment in assignmentsToRemove)
        {
            schedule.Assignments.Remove(assignment);
            _dbContext.ToolboxTalkScheduleAssignments.Remove(assignment);
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
                ProcessedAt = null
            };
            schedule.Assignments.Add(assignment);
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
