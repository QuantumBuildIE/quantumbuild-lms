using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetMyToolboxTalks;

public class GetMyToolboxTalksQueryHandler : IRequestHandler<GetMyToolboxTalksQuery, PaginatedList<MyToolboxTalkListDto>>
{
    private readonly IToolboxTalksDbContext _context;

    public GetMyToolboxTalksQueryHandler(IToolboxTalksDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<MyToolboxTalkListDto>> Handle(GetMyToolboxTalksQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var query = _context.ScheduledTalks
            .Where(st => st.TenantId == request.TenantId &&
                        st.EmployeeId == request.EmployeeId &&
                        !st.IsDeleted &&
                        st.Status != ScheduledTalkStatus.Cancelled)
            .AsQueryable();

        // Apply status filter
        if (request.Status.HasValue)
        {
            query = query.Where(st => st.Status == request.Status.Value);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply ordering (pending/in-progress first, then by due date) and pagination
        var items = await query
            .OrderBy(st => st.Status == ScheduledTalkStatus.Completed ? 1 : 0)
            .ThenBy(st => st.DueDate)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(st => new MyToolboxTalkListDto
            {
                ScheduledTalkId = st.Id,
                ToolboxTalkId = st.ToolboxTalkId,
                Code = st.ToolboxTalk.Code,
                Title = st.ToolboxTalk.Title,
                Description = st.ToolboxTalk.Description,
                RequiredDate = st.RequiredDate,
                DueDate = st.DueDate,
                Status = st.Status,
                StatusDisplay = GetStatusDisplay(st.Status),
                HasVideo = !string.IsNullOrEmpty(st.ToolboxTalk.VideoUrl),
                RequiresQuiz = st.ToolboxTalk.RequiresQuiz,
                TotalSections = st.ToolboxTalk.Sections.Count,
                CompletedSections = st.SectionProgress.Count(sp => sp.IsRead),
                ProgressPercent = st.ToolboxTalk.Sections.Count > 0
                    ? Math.Round((decimal)st.SectionProgress.Count(sp => sp.IsRead) / st.ToolboxTalk.Sections.Count * 100, 2)
                    : 0,
                IsOverdue = st.Status != ScheduledTalkStatus.Completed && st.DueDate < now,
                DaysUntilDue = (int)Math.Ceiling((st.DueDate - now).TotalDays),
                IsRefresher = st.IsRefresher,
                RefresherDueDate = st.RefresherDueDate
            })
            .ToListAsync(cancellationToken);

        return new PaginatedList<MyToolboxTalkListDto>(items, totalCount, request.PageNumber, request.PageSize);
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
