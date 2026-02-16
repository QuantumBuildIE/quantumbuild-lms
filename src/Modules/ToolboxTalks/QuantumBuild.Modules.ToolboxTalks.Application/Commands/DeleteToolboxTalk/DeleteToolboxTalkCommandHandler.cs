using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.DeleteToolboxTalk;

public class DeleteToolboxTalkCommandHandler : IRequestHandler<DeleteToolboxTalkCommand, bool>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public DeleteToolboxTalkCommandHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(DeleteToolboxTalkCommand request, CancellationToken cancellationToken)
    {
        var toolboxTalk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

        if (toolboxTalk == null)
        {
            throw new KeyNotFoundException($"Toolbox talk with ID {request.Id} not found.");
        }

        // Validate tenant ownership
        if (toolboxTalk.TenantId != request.TenantId)
        {
            throw new UnauthorizedAccessException("Access denied to this toolbox talk.");
        }

        // Check for active schedules that would be affected
        var hasActiveSchedules = await _dbContext.ToolboxTalkSchedules
            .AnyAsync(s => s.ToolboxTalkId == request.Id &&
                          s.Status == ToolboxTalkScheduleStatus.Active &&
                          !s.IsDeleted, cancellationToken);

        if (hasActiveSchedules)
        {
            throw new InvalidOperationException(
                "Cannot delete this toolbox talk because it has active schedules. " +
                "Please cancel or complete the schedules first, or deactivate the talk instead.");
        }

        // Check for pending or in-progress scheduled talks
        var hasPendingTalks = await _dbContext.ScheduledTalks
            .AnyAsync(st => st.ToolboxTalkId == request.Id &&
                           (st.Status == ScheduledTalkStatus.Pending ||
                            st.Status == ScheduledTalkStatus.InProgress) &&
                           !st.IsDeleted, cancellationToken);

        if (hasPendingTalks)
        {
            throw new InvalidOperationException(
                "Cannot delete this toolbox talk because it has pending or in-progress assignments. " +
                "Please wait for employees to complete their assignments or deactivate the talk instead.");
        }

        // Soft delete the toolbox talk
        toolboxTalk.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
