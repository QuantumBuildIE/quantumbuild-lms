using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Queries;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

public class UpdateToolboxTalkCourseCommandHandler : IRequestHandler<UpdateToolboxTalkCourseCommand, ToolboxTalkCourseDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IMediator _mediator;

    public UpdateToolboxTalkCourseCommandHandler(IToolboxTalksDbContext dbContext, IMediator mediator)
    {
        _dbContext = dbContext;
        _mediator = mediator;
    }

    public async Task<ToolboxTalkCourseDto> Handle(UpdateToolboxTalkCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _dbContext.ToolboxTalkCourses
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.TenantId == request.TenantId && !c.IsDeleted, cancellationToken);

        if (course == null)
        {
            throw new KeyNotFoundException($"Course with ID {request.Id} not found.");
        }

        var dto = request.Dto;

        // Validate title uniqueness (excluding current course)
        var titleExists = await _dbContext.ToolboxTalkCourses
            .AnyAsync(c => c.TenantId == request.TenantId && c.Title == dto.Title && c.Id != request.Id && !c.IsDeleted, cancellationToken);

        if (titleExists)
        {
            throw new InvalidOperationException($"A course with title '{dto.Title}' already exists.");
        }

        // Validate RefresherIntervalMonths
        if (dto.RequiresRefresher && dto.RefresherIntervalMonths < 1)
        {
            throw new InvalidOperationException("Refresher interval must be at least 1 month.");
        }

        course.Title = dto.Title;
        course.Description = dto.Description;
        course.IsActive = dto.IsActive;
        course.RequireSequentialCompletion = dto.RequireSequentialCompletion;
        course.RequiresRefresher = dto.RequiresRefresher;
        course.RefresherIntervalMonths = dto.RefresherIntervalMonths;
        course.GenerateCertificate = dto.GenerateCertificate;
        course.AutoAssignToNewEmployees = dto.AutoAssignToNewEmployees;
        course.AutoAssignDueDays = dto.AutoAssignDueDays;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Return the full course DTO by re-querying with includes
        var result = await _mediator.Send(new GetToolboxTalkCourseByIdQuery
        {
            Id = request.Id,
            TenantId = request.TenantId
        }, cancellationToken);

        return result!;
    }
}
