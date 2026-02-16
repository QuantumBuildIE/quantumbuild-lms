using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Queries;

public class GetToolboxTalkCoursesQueryHandler : IRequestHandler<GetToolboxTalkCoursesQuery, List<ToolboxTalkCourseListDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public GetToolboxTalkCoursesQueryHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ToolboxTalkCourseListDto>> Handle(GetToolboxTalkCoursesQuery request, CancellationToken cancellationToken)
    {
        var query = _dbContext.ToolboxTalkCourses
            .Where(c => c.TenantId == request.TenantId && !c.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchLower = request.SearchTerm.ToLower();
            query = query.Where(c => c.Title.ToLower().Contains(searchLower) ||
                                     (c.Description != null && c.Description.ToLower().Contains(searchLower)));
        }

        if (request.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == request.IsActive.Value);
        }

        var courses = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ToolboxTalkCourseListDto
            {
                Id = c.Id,
                Title = c.Title,
                Description = c.Description,
                IsActive = c.IsActive,
                RequireSequentialCompletion = c.RequireSequentialCompletion,
                AutoAssignToNewEmployees = c.AutoAssignToNewEmployees,
                TalkCount = c.CourseItems.Count(ci => !ci.IsDeleted),
                TranslationCount = c.Translations.Count(t => !t.IsDeleted),
                CreatedAt = c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return courses;
    }
}
