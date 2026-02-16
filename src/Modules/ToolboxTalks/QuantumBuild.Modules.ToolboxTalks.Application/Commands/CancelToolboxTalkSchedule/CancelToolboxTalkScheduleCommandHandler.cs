using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CancelToolboxTalkSchedule;

public class CancelToolboxTalkScheduleCommandHandler : IRequestHandler<CancelToolboxTalkScheduleCommand, bool>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public CancelToolboxTalkScheduleCommandHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(CancelToolboxTalkScheduleCommand request, CancellationToken cancellationToken)
    {
        // Get the schedule
        var schedule = await _dbContext.ToolboxTalkSchedules
            .FirstOrDefaultAsync(s => s.Id == request.Id && s.TenantId == request.TenantId, cancellationToken);

        if (schedule == null)
        {
            throw new InvalidOperationException($"Schedule with ID '{request.Id}' not found.");
        }

        // Check if already cancelled or completed
        if (schedule.Status == ToolboxTalkScheduleStatus.Cancelled)
        {
            throw new InvalidOperationException("Schedule is already cancelled.");
        }

        if (schedule.Status == ToolboxTalkScheduleStatus.Completed)
        {
            throw new InvalidOperationException("Cannot cancel a completed schedule.");
        }

        // Set schedule status to Cancelled
        schedule.Status = ToolboxTalkScheduleStatus.Cancelled;

        // Cancel pending scheduled talks if requested
        if (request.CancelPendingTalks)
        {
            var pendingTalks = await _dbContext.ScheduledTalks
                .Where(st => st.ScheduleId == request.Id &&
                             st.TenantId == request.TenantId &&
                             (st.Status == ScheduledTalkStatus.Pending || st.Status == ScheduledTalkStatus.InProgress))
                .ToListAsync(cancellationToken);

            foreach (var talk in pendingTalks)
            {
                talk.Status = ScheduledTalkStatus.Cancelled;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
