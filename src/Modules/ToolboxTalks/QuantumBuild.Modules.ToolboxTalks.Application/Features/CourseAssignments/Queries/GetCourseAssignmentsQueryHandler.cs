using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.Queries;

public class GetCourseAssignmentsQueryHandler : IRequestHandler<GetCourseAssignmentsQuery, List<CourseAssignmentListDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public GetCourseAssignmentsQueryHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<CourseAssignmentListDto>> Handle(GetCourseAssignmentsQuery request, CancellationToken cancellationToken)
    {
        var assignments = await _dbContext.ToolboxTalkCourseAssignments
            .Where(a => a.CourseId == request.CourseId && a.TenantId == request.TenantId && !a.IsDeleted)
            .Include(a => a.Course)
            .Include(a => a.Employee)
            .Include(a => a.ScheduledTalks.Where(st => !st.IsDeleted))
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new CourseAssignmentListDto
            {
                Id = a.Id,
                CourseId = a.CourseId,
                CourseTitle = a.Course.Title,
                EmployeeId = a.EmployeeId,
                EmployeeName = a.Employee.FirstName + " " + a.Employee.LastName,
                DueDate = a.DueDate,
                Status = a.Status.ToString(),
                TotalTalks = a.ScheduledTalks.Count(st => !st.IsDeleted),
                CompletedTalks = a.ScheduledTalks.Count(st => !st.IsDeleted && st.Status == ScheduledTalkStatus.Completed),
                AssignedAt = a.AssignedAt,
                CompletedAt = a.CompletedAt,
            })
            .ToListAsync(cancellationToken);

        return assignments;
    }
}
