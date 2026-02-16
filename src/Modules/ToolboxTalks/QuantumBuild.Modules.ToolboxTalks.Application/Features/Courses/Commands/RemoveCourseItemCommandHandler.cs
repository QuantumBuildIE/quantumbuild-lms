using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Queries;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

public class RemoveCourseItemCommandHandler : IRequestHandler<RemoveCourseItemCommand, ToolboxTalkCourseDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IMediator _mediator;

    public RemoveCourseItemCommandHandler(IToolboxTalksDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<ToolboxTalkCourseDto> Handle(RemoveCourseItemCommand request, CancellationToken cancellationToken)
    {
        // Verify course exists and belongs to tenant
        var courseExists = await _dbContext.ToolboxTalkCourses
            .AnyAsync(c => c.Id == request.CourseId && c.TenantId == request.TenantId && !c.IsDeleted, cancellationToken);

        if (!courseExists)
        {
            throw new KeyNotFoundException($"Course with ID {request.CourseId} not found.");
        }

        var item = await _dbContext.ToolboxTalkCourseItems
            .FirstOrDefaultAsync(ci => ci.CourseId == request.CourseId && ci.ToolboxTalkId == request.ToolboxTalkId && !ci.IsDeleted, cancellationToken);

        if (item == null)
        {
            throw new KeyNotFoundException($"Talk with ID {request.ToolboxTalkId} not found in course.");
        }

        item.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Return the updated course DTO
        var result = await _mediator.Send(new GetToolboxTalkCourseByIdQuery
        {
            Id = request.CourseId,
            TenantId = request.TenantId
        }, cancellationToken);

        return result!;
    }
}
