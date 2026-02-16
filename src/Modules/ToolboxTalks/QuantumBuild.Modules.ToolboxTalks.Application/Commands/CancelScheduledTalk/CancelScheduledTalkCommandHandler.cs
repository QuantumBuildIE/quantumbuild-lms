using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CancelScheduledTalk;

public class CancelScheduledTalkCommandHandler : IRequestHandler<CancelScheduledTalkCommand, bool>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public CancelScheduledTalkCommandHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> Handle(CancelScheduledTalkCommand request, CancellationToken cancellationToken)
    {
        // Get the scheduled talk
        var scheduledTalk = await _dbContext.ScheduledTalks
            .FirstOrDefaultAsync(st => st.Id == request.Id && st.TenantId == request.TenantId, cancellationToken);

        if (scheduledTalk == null)
        {
            throw new KeyNotFoundException($"Scheduled talk with ID '{request.Id}' not found.");
        }

        // Check if already cancelled
        if (scheduledTalk.Status == ScheduledTalkStatus.Cancelled)
        {
            throw new InvalidOperationException("Assignment is already cancelled.");
        }

        // Check if already completed
        if (scheduledTalk.Status == ScheduledTalkStatus.Completed)
        {
            throw new InvalidOperationException("Cannot cancel a completed assignment.");
        }

        // Set status to Cancelled
        scheduledTalk.Status = ScheduledTalkStatus.Cancelled;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}
