using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkScheduleById;

public class GetToolboxTalkScheduleByIdQueryHandler : IRequestHandler<GetToolboxTalkScheduleByIdQuery, ToolboxTalkScheduleDto?>
{
    private readonly IToolboxTalksDbContext _context;

    public GetToolboxTalkScheduleByIdQueryHandler(IToolboxTalksDbContext context)
    {
        _context = context;
    }

    public async Task<ToolboxTalkScheduleDto?> Handle(GetToolboxTalkScheduleByIdQuery request, CancellationToken cancellationToken)
    {
        var schedule = await _context.ToolboxTalkSchedules
            .Include(s => s.ToolboxTalk)
            .Include(s => s.Assignments)
                .ThenInclude(a => a.Employee)
            .Where(s => s.Id == request.Id && s.TenantId == request.TenantId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (schedule == null)
            return null;

        return new ToolboxTalkScheduleDto
        {
            Id = schedule.Id,
            ToolboxTalkId = schedule.ToolboxTalkId,
            ToolboxTalkCode = schedule.ToolboxTalk.Code,
            ToolboxTalkTitle = schedule.ToolboxTalk.Title,
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
                EmployeeName = a.Employee != null ? $"{a.Employee.FirstName} {a.Employee.LastName}".Trim() : $"Employee {a.EmployeeId}",
                EmployeeEmail = a.Employee?.Email,
                IsProcessed = a.IsProcessed,
                ProcessedAt = a.ProcessedAt
            }).ToList(),
            CreatedAt = schedule.CreatedAt,
            UpdatedAt = schedule.UpdatedAt
        };
    }

    private static string GetFrequencyDisplay(ToolboxTalkFrequency frequency) => frequency switch
    {
        ToolboxTalkFrequency.Once => "One-time",
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
