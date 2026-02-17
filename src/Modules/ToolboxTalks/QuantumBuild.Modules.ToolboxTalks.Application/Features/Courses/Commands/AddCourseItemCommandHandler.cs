using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Queries;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

public class AddCourseItemCommandHandler : IRequestHandler<AddCourseItemCommand, ToolboxTalkCourseDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IMediator _mediator;

    public AddCourseItemCommandHandler(IToolboxTalksDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<ToolboxTalkCourseDto> Handle(AddCourseItemCommand request, CancellationToken cancellationToken)
    {
        var course = await _dbContext.ToolboxTalkCourses
            .FirstOrDefaultAsync(c => c.Id == request.CourseId && c.TenantId == request.TenantId && !c.IsDeleted, cancellationToken);

        if (course == null)
        {
            throw new KeyNotFoundException($"Course with ID {request.CourseId} not found.");
        }

        var dto = request.Dto;

        // Validate talk exists
        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == dto.ToolboxTalkId && t.TenantId == request.TenantId && !t.IsDeleted, cancellationToken);

        if (talk == null)
        {
            throw new KeyNotFoundException($"Learning with ID {dto.ToolboxTalkId} not found.");
        }

        // Check for duplicate
        var alreadyExists = await _dbContext.ToolboxTalkCourseItems
            .AnyAsync(ci => ci.CourseId == request.CourseId && ci.ToolboxTalkId == dto.ToolboxTalkId && !ci.IsDeleted, cancellationToken);

        if (alreadyExists)
        {
            throw new InvalidOperationException("This talk is already part of the course.");
        }

        var item = new ToolboxTalkCourseItem
        {
            Id = Guid.NewGuid(),
            CourseId = request.CourseId,
            ToolboxTalkId = dto.ToolboxTalkId,
            OrderIndex = dto.OrderIndex,
            IsRequired = dto.IsRequired
        };

        _dbContext.ToolboxTalkCourseItems.Add(item);
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
