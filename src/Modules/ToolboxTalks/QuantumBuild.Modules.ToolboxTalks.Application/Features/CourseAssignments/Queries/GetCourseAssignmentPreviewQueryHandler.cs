using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.Queries;

public class GetCourseAssignmentPreviewQueryHandler
    : IRequestHandler<GetCourseAssignmentPreviewQuery, CourseAssignmentPreviewDto?>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;

    public GetCourseAssignmentPreviewQueryHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
    }

    public async Task<CourseAssignmentPreviewDto?> Handle(
        GetCourseAssignmentPreviewQuery request, CancellationToken cancellationToken)
    {
        var tenantId = request.TenantId;

        // Load course with items
        var course = await _dbContext.ToolboxTalkCourses
            .Include(c => c.CourseItems.Where(ci => !ci.IsDeleted))
                .ThenInclude(ci => ci.ToolboxTalk)
            .FirstOrDefaultAsync(c => c.Id == request.CourseId
                && c.TenantId == tenantId
                && !c.IsDeleted, cancellationToken);

        if (course == null)
            return null;

        // Load employees
        var employees = await _coreDbContext.Employees
            .Where(e => request.EmployeeIds.Contains(e.Id)
                && e.TenantId == tenantId
                && !e.IsDeleted)
            .Select(e => new { e.Id, e.FirstName, e.LastName, e.EmployeeCode })
            .ToListAsync(cancellationToken);

        // Get talk IDs in the course
        var courseTalkIds = course.CourseItems
            .Where(ci => ci.ToolboxTalk != null && !ci.ToolboxTalk.IsDeleted)
            .Select(ci => ci.ToolboxTalkId)
            .ToHashSet();

        // Find completed talks for these employees (any completion, not just from this course)
        var completedTalks = await _dbContext.ScheduledTalks
            .Where(st => request.EmployeeIds.Contains(st.EmployeeId)
                && courseTalkIds.Contains(st.ToolboxTalkId)
                && st.Status == ScheduledTalkStatus.Completed
                && st.TenantId == tenantId
                && !st.IsDeleted)
            .Include(st => st.Completion)
            .Select(st => new
            {
                st.EmployeeId,
                st.ToolboxTalkId,
                CompletedAt = st.Completion != null ? (DateTime?)st.Completion.CompletedAt : null
            })
            .ToListAsync(cancellationToken);

        // Group by employee, taking the most recent completion per talk
        var completedByEmployee = completedTalks
            .GroupBy(x => x.EmployeeId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.ToolboxTalkId)
                    .ToDictionary(
                        tg => tg.Key,
                        tg => tg.Max(x => x.CompletedAt)));

        var courseItems = course.CourseItems
            .Where(ci => ci.ToolboxTalk != null && !ci.ToolboxTalk.IsDeleted)
            .OrderBy(ci => ci.OrderIndex)
            .ToList();

        var result = new CourseAssignmentPreviewDto
        {
            CourseId = course.Id,
            CourseTitle = course.Title,
            Employees = employees.Select(emp =>
            {
                var empCompleted = completedByEmployee.GetValueOrDefault(emp.Id)
                    ?? new Dictionary<Guid, DateTime?>();

                var talks = courseItems
                    .Select(ci => new CourseTalkPreviewDto
                    {
                        ToolboxTalkId = ci.ToolboxTalkId,
                        Title = ci.ToolboxTalk!.Title,
                        OrderIndex = ci.OrderIndex,
                        IsRequired = ci.IsRequired,
                        AlreadyCompleted = empCompleted.ContainsKey(ci.ToolboxTalkId),
                        CompletedAt = empCompleted.GetValueOrDefault(ci.ToolboxTalkId),
                    }).ToList();

                return new CourseAssignmentEmployeePreviewDto
                {
                    EmployeeId = emp.Id,
                    EmployeeName = $"{emp.FirstName} {emp.LastName}",
                    EmployeeCode = emp.EmployeeCode,
                    Talks = talks,
                    CompletedCount = talks.Count(t => t.AlreadyCompleted),
                    TotalCount = talks.Count,
                };
            }).ToList()
        };

        return result;
    }
}
