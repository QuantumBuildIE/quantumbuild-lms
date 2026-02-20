using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkSchedules;

public class GetToolboxTalkSchedulesQueryHandler : IRequestHandler<GetToolboxTalkSchedulesQuery, PaginatedList<ToolboxTalkScheduleListDto>>
{
    private readonly IToolboxTalksDbContext _context;

    public GetToolboxTalkSchedulesQueryHandler(IToolboxTalksDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ToolboxTalkScheduleListDto>> Handle(GetToolboxTalkSchedulesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ToolboxTalkSchedules
            .Include(s => s.ToolboxTalk)
            .Include(s => s.Assignments)
            .Where(s => s.TenantId == request.TenantId && !s.IsDeleted)
            .AsQueryable();

        // Apply toolbox talk filter
        if (request.ToolboxTalkId.HasValue)
        {
            query = query.Where(s => s.ToolboxTalkId == request.ToolboxTalkId.Value);
        }

        // Apply status filter
        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }

        // Apply date range filters
        if (request.DateFrom.HasValue)
        {
            query = query.Where(s => s.ScheduledDate >= request.DateFrom.Value);
        }

        if (request.DateTo.HasValue)
        {
            query = query.Where(s => s.ScheduledDate <= request.DateTo.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply ordering and pagination
        var items = await query
            .OrderByDescending(s => s.ScheduledDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new ToolboxTalkScheduleListDto
            {
                Id = s.Id,
                ToolboxTalkId = s.ToolboxTalkId,
                ToolboxTalkCode = s.ToolboxTalk.Code,
                ToolboxTalkTitle = s.ToolboxTalk.Title,
                ScheduledDate = s.ScheduledDate,
                EndDate = s.EndDate,
                Frequency = s.Frequency,
                FrequencyDisplay = GetFrequencyDisplay(s.Frequency),
                AssignToAllEmployees = s.AssignToAllEmployees,
                Status = s.Status,
                StatusDisplay = GetStatusDisplay(s.Status),
                NextRunDate = s.NextRunDate,
                AssignmentCount = s.Assignments.Count,
                ProcessedCount = s.Assignments.Count(a => a.IsProcessed),
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<ToolboxTalkScheduleListDto>(items, totalCount, request.PageNumber, request.PageSize);
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
