using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Queries;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

public class UpdateCourseItemsCommandHandler : IRequestHandler<UpdateCourseItemsCommand, ToolboxTalkCourseDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IMediator _mediator;

    public UpdateCourseItemsCommandHandler(IToolboxTalksDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<ToolboxTalkCourseDto> Handle(UpdateCourseItemsCommand request, CancellationToken cancellationToken)
    {
        var course = await _dbContext.ToolboxTalkCourses
            .FirstOrDefaultAsync(c => c.Id == request.CourseId && c.TenantId == request.TenantId && !c.IsDeleted, cancellationToken);

        if (course == null)
        {
            throw new KeyNotFoundException($"Course with ID {request.CourseId} not found.");
        }

        var dto = request.Dto;

        // Check for duplicates within the request
        var duplicateTalkIds = dto.Items.GroupBy(i => i.ToolboxTalkId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateTalkIds.Any())
        {
            throw new InvalidOperationException("A talk cannot appear more than once in a course.");
        }

        // Validate all talks exist
        var requestedTalkIds = dto.Items.Select(i => i.ToolboxTalkId).Distinct().ToList();
        if (requestedTalkIds.Any())
        {
            var existingTalkIds = await _dbContext.ToolboxTalks
                .Where(t => requestedTalkIds.Contains(t.Id) && t.TenantId == request.TenantId && !t.IsDeleted)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            var missingIds = requestedTalkIds.Except(existingTalkIds).ToList();
            if (missingIds.Any())
            {
                throw new InvalidOperationException($"One or more learnings not found: {string.Join(", ", missingIds)}");
            }
        }

        // Get existing items
        var existingItems = await _dbContext.ToolboxTalkCourseItems
            .Where(ci => ci.CourseId == request.CourseId && !ci.IsDeleted)
            .ToListAsync(cancellationToken);

        // Soft-delete items that are not in the new list
        var newTalkIds = dto.Items.Select(i => i.ToolboxTalkId).ToHashSet();
        foreach (var existingItem in existingItems)
        {
            if (!newTalkIds.Contains(existingItem.ToolboxTalkId))
            {
                existingItem.IsDeleted = true;
            }
        }

        // Update existing items and add new ones
        foreach (var itemDto in dto.Items)
        {
            var existingItem = existingItems.FirstOrDefault(ci => ci.ToolboxTalkId == itemDto.ToolboxTalkId);
            if (existingItem != null)
            {
                existingItem.OrderIndex = itemDto.OrderIndex;
                existingItem.IsRequired = itemDto.IsRequired;
            }
            else
            {
                var newItem = new ToolboxTalkCourseItem
                {
                    Id = Guid.NewGuid(),
                    CourseId = request.CourseId,
                    ToolboxTalkId = itemDto.ToolboxTalkId,
                    OrderIndex = itemDto.OrderIndex,
                    IsRequired = itemDto.IsRequired
                };
                _dbContext.ToolboxTalkCourseItems.Add(newItem);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Return the full course DTO
        var result = await _mediator.Send(new GetToolboxTalkCourseByIdQuery
        {
            Id = request.CourseId,
            TenantId = request.TenantId
        }, cancellationToken);

        return result!;
    }
}
