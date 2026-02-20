using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetScheduledTalks;

public class GetScheduledTalksQueryHandler : IRequestHandler<GetScheduledTalksQuery, PaginatedList<ScheduledTalkListDto>>
{
    private readonly IToolboxTalksDbContext _context;

    public GetScheduledTalksQueryHandler(IToolboxTalksDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<ScheduledTalkListDto>> Handle(GetScheduledTalksQuery request, CancellationToken cancellationToken)
    {
        var query = _context.ScheduledTalks
            .Where(st => st.TenantId == request.TenantId && !st.IsDeleted)
            .AsQueryable();

        // Apply employee filter
        if (request.EmployeeId.HasValue)
        {
            query = query.Where(st => st.EmployeeId == request.EmployeeId.Value);
        }

        // Apply toolbox talk filter
        if (request.ToolboxTalkId.HasValue)
        {
            query = query.Where(st => st.ToolboxTalkId == request.ToolboxTalkId.Value);
        }

        // Apply status filter
        if (request.Status.HasValue)
        {
            query = query.Where(st => st.Status == request.Status.Value);
        }

        // Apply due date range filters
        if (request.DueDateFrom.HasValue)
        {
            query = query.Where(st => st.DueDate >= request.DueDateFrom.Value);
        }

        if (request.DueDateTo.HasValue)
        {
            query = query.Where(st => st.DueDate <= request.DueDateTo.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply ordering and pagination
        var items = await query
            .OrderByDescending(st => st.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(st => new ScheduledTalkListDto
            {
                Id = st.Id,
                ToolboxTalkId = st.ToolboxTalkId,
                ToolboxTalkCode = st.ToolboxTalk.Code,
                ToolboxTalkTitle = st.ToolboxTalk.Title,
                EmployeeId = st.EmployeeId,
                EmployeeName = st.Employee.FirstName + " " + st.Employee.LastName,
                EmployeeEmail = st.Employee.Email,
                ScheduleId = st.ScheduleId,
                RequiredDate = st.RequiredDate,
                DueDate = st.DueDate,
                Status = st.Status,
                StatusDisplay = GetStatusDisplay(st.Status),
                RemindersSent = st.RemindersSent,
                TotalSections = st.ToolboxTalk.Sections.Count,
                CompletedSections = st.SectionProgress.Count(sp => sp.IsRead),
                ProgressPercent = st.ToolboxTalk.Sections.Count > 0
                    ? Math.Round((decimal)st.SectionProgress.Count(sp => sp.IsRead) / st.ToolboxTalk.Sections.Count * 100, 2)
                    : 0,
                CreatedAt = st.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<ScheduledTalkListDto>(items, totalCount, request.PageNumber, request.PageSize);
    }

    private static string GetStatusDisplay(ScheduledTalkStatus status) => status switch
    {
        ScheduledTalkStatus.Pending => "Pending",
        ScheduledTalkStatus.InProgress => "In Progress",
        ScheduledTalkStatus.Completed => "Completed",
        ScheduledTalkStatus.Overdue => "Overdue",
        ScheduledTalkStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };
}
