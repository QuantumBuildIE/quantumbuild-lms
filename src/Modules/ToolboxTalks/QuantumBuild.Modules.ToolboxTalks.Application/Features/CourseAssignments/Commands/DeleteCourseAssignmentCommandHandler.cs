using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.Commands;

public class DeleteCourseAssignmentCommandHandler : IRequestHandler<DeleteCourseAssignmentCommand, bool>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public DeleteCourseAssignmentCommandHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteCourseAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await _dbContext.ToolboxTalkCourseAssignments
            .Include(a => a.ScheduledTalks.Where(st => !st.IsDeleted))
            .FirstOrDefaultAsync(a => a.Id == request.Id && !a.IsDeleted, cancellationToken);

        if (assignment == null)
            throw new KeyNotFoundException($"Course assignment with ID {request.Id} not found.");

        if (assignment.TenantId != request.TenantId)
            throw new UnauthorizedAccessException("Access denied to this course assignment.");

        if (assignment.Status == CourseAssignmentStatus.Completed)
            throw new InvalidOperationException("Cannot delete a completed course assignment.");

        // Soft delete the assignment and all its scheduled talks
        assignment.IsDeleted = true;
        foreach (var scheduledTalk in assignment.ScheduledTalks)
        {
            scheduledTalk.IsDeleted = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
