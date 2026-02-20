using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Queries;

public class GetToolboxTalkCourseByIdQueryHandler : IRequestHandler<GetToolboxTalkCourseByIdQuery, ToolboxTalkCourseDto?>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public GetToolboxTalkCourseByIdQueryHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ToolboxTalkCourseDto?> Handle(GetToolboxTalkCourseByIdQuery request, CancellationToken cancellationToken)
    {
        var course = await _dbContext.ToolboxTalkCourses
            .Include(c => c.CourseItems.Where(ci => !ci.IsDeleted))
                .ThenInclude(ci => ci.ToolboxTalk)
                    .ThenInclude(t => t.Sections)
            .Include(c => c.CourseItems.Where(ci => !ci.IsDeleted))
                .ThenInclude(ci => ci.ToolboxTalk)
                    .ThenInclude(t => t.Questions)
            .Include(c => c.Translations.Where(t => !t.IsDeleted))
            .Where(c => c.Id == request.Id && c.TenantId == request.TenantId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (course == null)
            return null;

        return new ToolboxTalkCourseDto
        {
            Id = course.Id,
            Title = course.Title,
            Description = course.Description,
            IsActive = course.IsActive,
            RequireSequentialCompletion = course.RequireSequentialCompletion,
            RequiresRefresher = course.RequiresRefresher,
            RefresherIntervalMonths = course.RefresherIntervalMonths,
            GenerateCertificate = course.GenerateCertificate,
            AutoAssignToNewEmployees = course.AutoAssignToNewEmployees,
            AutoAssignDueDays = course.AutoAssignDueDays,
            TalkCount = course.CourseItems.Count(ci => ci.ToolboxTalk != null),
            Items = course.CourseItems
                .Where(ci => ci.ToolboxTalk != null)
                .OrderBy(ci => ci.OrderIndex)
                .Select(ci => new ToolboxTalkCourseItemDto
                {
                    Id = ci.Id,
                    ToolboxTalkId = ci.ToolboxTalkId,
                    OrderIndex = ci.OrderIndex,
                    IsRequired = ci.IsRequired,
                    TalkCode = ci.ToolboxTalk!.Code,
                    TalkTitle = ci.ToolboxTalk!.Title,
                    TalkDescription = ci.ToolboxTalk.Description,
                    TalkHasVideo = !string.IsNullOrEmpty(ci.ToolboxTalk.VideoUrl),
                    TalkSectionCount = ci.ToolboxTalk.Sections?.Count(s => !s.IsDeleted) ?? 0,
                    TalkQuestionCount = ci.ToolboxTalk.Questions?.Count(q => !q.IsDeleted) ?? 0
                })
                .ToList(),
            Translations = course.Translations
                .Select(t => new ToolboxTalkCourseTranslationDto
                {
                    Id = t.Id,
                    LanguageCode = t.LanguageCode,
                    TranslatedTitle = t.TranslatedTitle,
                    TranslatedDescription = t.TranslatedDescription
                })
                .ToList(),
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt
        };
    }
}
