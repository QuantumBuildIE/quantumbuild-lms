using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateVideoProgress;

public class UpdateVideoProgressCommandHandler : IRequestHandler<UpdateVideoProgressCommand, VideoProgressDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpdateVideoProgressCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<VideoProgressDto> Handle(UpdateVideoProgressCommand request, CancellationToken cancellationToken)
    {
        // Get the current user's employee record
        var employee = await _coreDbContext.Employees
            .FirstOrDefaultAsync(e => e.UserId == _currentUserService.UserId &&
                                      e.TenantId == _currentUserService.TenantId &&
                                      !e.IsDeleted,
                                 cancellationToken);

        if (employee == null)
        {
            throw new InvalidOperationException("No employee record found for the current user.");
        }

        // Get the scheduled talk with toolbox talk
        var scheduledTalk = await _dbContext.ScheduledTalks
            .Include(st => st.ToolboxTalk)
            .FirstOrDefaultAsync(st => st.Id == request.ScheduledTalkId &&
                                       st.TenantId == _currentUserService.TenantId,
                                 cancellationToken);

        if (scheduledTalk == null)
        {
            throw new InvalidOperationException($"Scheduled talk with ID '{request.ScheduledTalkId}' not found.");
        }

        // Validate the scheduled talk belongs to the current user's employee
        if (scheduledTalk.EmployeeId != employee.Id)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this scheduled talk.");
        }

        // Validate the scheduled talk is not already completed or cancelled
        if (scheduledTalk.Status == ScheduledTalkStatus.Completed)
        {
            throw new InvalidOperationException("This scheduled talk has already been completed.");
        }

        if (scheduledTalk.Status == ScheduledTalkStatus.Cancelled)
        {
            throw new InvalidOperationException("This scheduled talk has been cancelled.");
        }

        // Validate there is a video for this talk
        if (string.IsNullOrEmpty(scheduledTalk.ToolboxTalk.VideoUrl))
        {
            throw new InvalidOperationException("This toolbox talk does not have a video.");
        }

        // Update video progress (only increase, never decrease)
        if (request.WatchPercent > scheduledTalk.VideoWatchPercent)
        {
            scheduledTalk.VideoWatchPercent = Math.Min(request.WatchPercent, 100);
        }

        // If this is the first interaction, update status to InProgress
        if (scheduledTalk.Status == ScheduledTalkStatus.Pending)
        {
            scheduledTalk.Status = ScheduledTalkStatus.InProgress;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var minimumWatchPercent = scheduledTalk.ToolboxTalk.MinimumVideoWatchPercent;

        return new VideoProgressDto
        {
            WatchPercent = scheduledTalk.VideoWatchPercent,
            MinimumWatchPercent = minimumWatchPercent,
            RequirementMet = scheduledTalk.VideoWatchPercent >= minimumWatchPercent
        };
    }
}
