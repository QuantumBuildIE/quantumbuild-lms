using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.Queries;

public class GetEmployeeCourseAssignmentsQueryHandler : IRequestHandler<GetEmployeeCourseAssignmentsQuery, List<ToolboxTalkCourseAssignmentDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public GetEmployeeCourseAssignmentsQueryHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ToolboxTalkCourseAssignmentDto>> Handle(GetEmployeeCourseAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var assignments = await _dbContext.ToolboxTalkCourseAssignments
            .Where(a => a.EmployeeId == request.EmployeeId && a.TenantId == request.TenantId && !a.IsDeleted)
            .Include(a => a.Course)
            .Include(a => a.Employee)
            .Include(a => a.ScheduledTalks.Where(st => !st.IsDeleted))
                .ThenInclude(st => st.ToolboxTalk)
            .Include(a => a.ScheduledTalks.Where(st => !st.IsDeleted))
                .ThenInclude(st => st.Completion)
            .OrderByDescending(a => a.Status != CourseAssignmentStatus.Completed)
            .ThenByDescending(a => a.AssignedAt)
            .ToListAsync(cancellationToken);

        var results = new List<ToolboxTalkCourseAssignmentDto>();

        foreach (var assignment in assignments)
        {
            var orderedTalks = assignment.ScheduledTalks
                .OrderBy(st => st.CourseOrderIndex)
                .ToList();

            var scheduledTalkDtos = new List<CourseScheduledTalkDto>();

            for (int i = 0; i < orderedTalks.Count; i++)
            {
                var talk = orderedTalks[i];
                var isLocked = false;
                string? lockedReason = null;

                if (assignment.Course.RequireSequentialCompletion && i > 0)
                {
                    var previousUncompletedRequired = orderedTalks
                        .Take(i)
                        .Where(t => t.Status != ScheduledTalkStatus.Completed)
                        .FirstOrDefault();

                    if (previousUncompletedRequired != null)
                    {
                        isLocked = true;
                        lockedReason = $"Complete '{previousUncompletedRequired.ToolboxTalk?.Title ?? "previous talk"}' first";
                    }
                }

                scheduledTalkDtos.Add(new CourseScheduledTalkDto
                {
                    ScheduledTalkId = talk.Id,
                    ToolboxTalkId = talk.ToolboxTalkId,
                    TalkTitle = talk.ToolboxTalk?.Title ?? string.Empty,
                    OrderIndex = talk.CourseOrderIndex ?? i,
                    IsRequired = true,
                    Status = talk.Status.ToString(),
                    CompletedAt = talk.Completion?.CompletedAt,
                    IsLocked = isLocked,
                    LockedReason = lockedReason,
                });
            }

            var completedCount = orderedTalks.Count(st => st.Status == ScheduledTalkStatus.Completed);

            results.Add(new ToolboxTalkCourseAssignmentDto
            {
                Id = assignment.Id,
                CourseId = assignment.CourseId,
                CourseTitle = assignment.Course.Title,
                CourseDescription = assignment.Course.Description,
                EmployeeId = assignment.EmployeeId,
                EmployeeName = $"{assignment.Employee.FirstName} {assignment.Employee.LastName}",
                EmployeeCode = assignment.Employee.EmployeeCode,
                AssignedAt = assignment.AssignedAt,
                DueDate = assignment.DueDate,
                StartedAt = assignment.StartedAt,
                CompletedAt = assignment.CompletedAt,
                Status = assignment.Status.ToString(),
                IsRefresher = assignment.IsRefresher,
                RefresherDueDate = assignment.RefresherDueDate,
                TotalTalks = orderedTalks.Count,
                CompletedTalks = completedCount,
                ScheduledTalks = scheduledTalkDtos,
            });
        }

        return results;
    }
}
