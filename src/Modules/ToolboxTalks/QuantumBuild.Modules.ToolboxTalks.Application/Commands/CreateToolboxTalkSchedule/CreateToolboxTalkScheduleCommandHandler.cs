using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CreateToolboxTalkSchedule;

public class CreateToolboxTalkScheduleCommandHandler : IRequestHandler<CreateToolboxTalkScheduleCommand, ToolboxTalkScheduleDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;

    public CreateToolboxTalkScheduleCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
    }

    public async Task<ToolboxTalkScheduleDto> Handle(CreateToolboxTalkScheduleCommand request, CancellationToken cancellationToken)
    {
        // Validate toolbox talk exists and is active
        var toolboxTalk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == request.ToolboxTalkId && t.TenantId == request.TenantId, cancellationToken);

        if (toolboxTalk == null)
        {
            throw new InvalidOperationException($"Toolbox talk with ID '{request.ToolboxTalkId}' not found.");
        }

        if (!toolboxTalk.IsActive)
        {
            throw new InvalidOperationException($"Toolbox talk '{toolboxTalk.Title}' is not active and cannot be scheduled.");
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

        // Create the schedule
        var schedule = new ToolboxTalkSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            ToolboxTalkId = request.ToolboxTalkId,
            ScheduledDate = request.ScheduledDate,
            EndDate = request.EndDate,
            Frequency = request.Frequency,
            AssignToAllEmployees = request.AssignToAllEmployees,
            Status = ToolboxTalkScheduleStatus.Draft,
            NextRunDate = request.ScheduledDate,
            Notes = request.Notes
        };

        // Create assignments
        foreach (var employeeId in employeeIdsToAssign)
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

        _dbContext.ToolboxTalkSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Return the created schedule as DTO
        return MapToDto(schedule, toolboxTalk.Title);
    }

    private static ToolboxTalkScheduleDto MapToDto(ToolboxTalkSchedule schedule, string toolboxTalkTitle)
    {
        return new ToolboxTalkScheduleDto
        {
            Id = schedule.Id,
            ToolboxTalkId = schedule.ToolboxTalkId,
            ToolboxTalkTitle = toolboxTalkTitle,
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
                EmployeeName = string.Empty, // Will be populated by query if needed
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
