using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

public class CreateToolboxTalkCourseCommandHandler : IRequestHandler<CreateToolboxTalkCourseCommand, ToolboxTalkCourseDto>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public CreateToolboxTalkCourseCommandHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ToolboxTalkCourseDto> Handle(CreateToolboxTalkCourseCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        // Validate title is unique within tenant
        var titleExists = await _dbContext.ToolboxTalkCourses
            .AnyAsync(c => c.TenantId == request.TenantId && c.Title == dto.Title && !c.IsDeleted, cancellationToken);

        if (titleExists)
        {
            throw new InvalidOperationException($"A course with title '{dto.Title}' already exists.");
        }

        // Validate RefresherIntervalMonths
        if (dto.RequiresRefresher && dto.RefresherIntervalMonths < 1)
        {
            throw new InvalidOperationException("Refresher interval must be at least 1 month.");
        }

        var course = new ToolboxTalkCourse
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Title = dto.Title,
            Description = dto.Description,
            IsActive = dto.IsActive,
            RequireSequentialCompletion = dto.RequireSequentialCompletion,
            RequiresRefresher = dto.RequiresRefresher,
            RefresherIntervalMonths = dto.RefresherIntervalMonths,
            GenerateCertificate = dto.GenerateCertificate,
            AutoAssignToNewEmployees = dto.AutoAssignToNewEmployees,
            AutoAssignDueDays = dto.AutoAssignDueDays
        };

        // Add items if provided
        var itemDtos = new List<ToolboxTalkCourseItemDto>();
        if (dto.Items != null && dto.Items.Count > 0)
        {
            // Validate all talks exist and are active
            var talkIds = dto.Items.Select(i => i.ToolboxTalkId).Distinct().ToList();
            var talks = await _dbContext.ToolboxTalks
                .Where(t => talkIds.Contains(t.Id) && t.TenantId == request.TenantId && !t.IsDeleted)
                .ToListAsync(cancellationToken);

            if (talks.Count != talkIds.Count)
            {
                var missingIds = talkIds.Except(talks.Select(t => t.Id));
                throw new InvalidOperationException($"One or more learnings not found: {string.Join(", ", missingIds)}");
            }

            // Check for duplicates within the request
            var duplicateTalkIds = dto.Items.GroupBy(i => i.ToolboxTalkId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicateTalkIds.Any())
            {
                throw new InvalidOperationException("A talk cannot be added to the same course more than once.");
            }

            foreach (var itemDto in dto.Items)
            {
                var talk = talks.First(t => t.Id == itemDto.ToolboxTalkId);
                var item = new ToolboxTalkCourseItem
                {
                    Id = Guid.NewGuid(),
                    CourseId = course.Id,
                    ToolboxTalkId = itemDto.ToolboxTalkId,
                    OrderIndex = itemDto.OrderIndex,
                    IsRequired = itemDto.IsRequired
                };
                course.CourseItems.Add(item);

                itemDtos.Add(new ToolboxTalkCourseItemDto
                {
                    Id = item.Id,
                    ToolboxTalkId = item.ToolboxTalkId,
                    OrderIndex = item.OrderIndex,
                    IsRequired = item.IsRequired,
                    TalkTitle = talk.Title,
                    TalkDescription = talk.Description,
                    TalkHasVideo = !string.IsNullOrEmpty(talk.VideoUrl),
                    TalkSectionCount = 0,
                    TalkQuestionCount = 0
                });
            }
        }

        _dbContext.ToolboxTalkCourses.Add(course);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
            TalkCount = course.CourseItems.Count,
            Items = itemDtos.OrderBy(i => i.OrderIndex).ToList(),
            Translations = new List<ToolboxTalkCourseTranslationDto>(),
            CreatedAt = course.CreatedAt,
            UpdatedAt = course.UpdatedAt
        };
    }
}
