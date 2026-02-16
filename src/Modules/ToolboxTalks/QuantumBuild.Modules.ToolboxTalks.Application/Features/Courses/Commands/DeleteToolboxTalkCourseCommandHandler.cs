using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;

public class DeleteToolboxTalkCourseCommandHandler : IRequestHandler<DeleteToolboxTalkCourseCommand, bool>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public DeleteToolboxTalkCourseCommandHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteToolboxTalkCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await _dbContext.ToolboxTalkCourses
            .FirstOrDefaultAsync(c => c.Id == request.Id && !c.IsDeleted, cancellationToken);

        if (course == null)
        {
            throw new KeyNotFoundException($"Course with ID {request.Id} not found.");
        }

        if (course.TenantId != request.TenantId)
        {
            throw new UnauthorizedAccessException("Access denied to this course.");
        }

        course.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
