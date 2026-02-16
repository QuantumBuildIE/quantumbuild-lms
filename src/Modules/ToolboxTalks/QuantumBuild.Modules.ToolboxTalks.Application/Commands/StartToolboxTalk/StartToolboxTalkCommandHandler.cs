using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.StartToolboxTalk;

public class StartToolboxTalkCommandHandler : IRequestHandler<StartToolboxTalkCommand, Unit>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;
    private readonly ICurrentUserService _currentUserService;

    public StartToolboxTalkCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<Unit> Handle(StartToolboxTalkCommand request, CancellationToken cancellationToken)
    {
        var employee = await _coreDbContext.Employees
            .FirstOrDefaultAsync(e => e.UserId == _currentUserService.UserId &&
                                      e.TenantId == _currentUserService.TenantId &&
                                      !e.IsDeleted,
                                 cancellationToken);

        if (employee == null)
        {
            throw new InvalidOperationException("No employee record found for the current user.");
        }

        var scheduledTalk = await _dbContext.ScheduledTalks
            .FirstOrDefaultAsync(st => st.Id == request.ScheduledTalkId &&
                                       st.TenantId == _currentUserService.TenantId,
                                 cancellationToken);

        if (scheduledTalk == null)
        {
            throw new InvalidOperationException($"Scheduled talk with ID '{request.ScheduledTalkId}' not found.");
        }

        if (scheduledTalk.EmployeeId != employee.Id)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this scheduled talk.");
        }

        // Only record start location once (idempotent - ignore if already started)
        if (scheduledTalk.StartedLocationTimestamp.HasValue)
        {
            return Unit.Value;
        }

        // Cancelled/Completed talks cannot be started
        if (scheduledTalk.Status == ScheduledTalkStatus.Completed ||
            scheduledTalk.Status == ScheduledTalkStatus.Cancelled)
        {
            return Unit.Value;
        }

        // Transition to InProgress if still Pending
        if (scheduledTalk.Status == ScheduledTalkStatus.Pending)
        {
            scheduledTalk.Status = ScheduledTalkStatus.InProgress;
        }

        // Record geolocation
        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            scheduledTalk.StartedLatitude = request.Latitude;
            scheduledTalk.StartedLongitude = request.Longitude;
            scheduledTalk.StartedAccuracyMeters = request.AccuracyMeters;
        }
        scheduledTalk.StartedLocationTimestamp = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
