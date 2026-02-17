using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.MarkSectionRead;

public class MarkSectionReadCommandHandler : IRequestHandler<MarkSectionReadCommand, ScheduledTalkSectionProgressDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;
    private readonly ICurrentUserService _currentUserService;

    public MarkSectionReadCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<ScheduledTalkSectionProgressDto> Handle(MarkSectionReadCommand request, CancellationToken cancellationToken)
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

        // Get the scheduled talk with section progress and toolbox talk sections
        var scheduledTalk = await _dbContext.ScheduledTalks
            .Include(st => st.SectionProgress)
            .Include(st => st.ToolboxTalk)
                .ThenInclude(t => t.Sections)
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

        // Validate the section belongs to this toolbox talk
        var section = scheduledTalk.ToolboxTalk.Sections
            .FirstOrDefault(s => s.Id == request.SectionId);

        if (section == null)
        {
            throw new InvalidOperationException($"Section with ID '{request.SectionId}' does not belong to this learning.");
        }

        // Validate sequential order - all previous sections must be read
        var orderedSections = scheduledTalk.ToolboxTalk.Sections
            .OrderBy(s => s.SectionNumber)
            .ToList();

        var currentSectionIndex = orderedSections.FindIndex(s => s.Id == request.SectionId);

        for (var i = 0; i < currentSectionIndex; i++)
        {
            var previousSection = orderedSections[i];
            var previousProgress = scheduledTalk.SectionProgress
                .FirstOrDefault(p => p.SectionId == previousSection.Id);

            if (previousProgress == null || !previousProgress.IsRead)
            {
                throw new InvalidOperationException($"You must read section '{previousSection.Title}' before reading this section.");
            }
        }

        // Find or create the section progress record
        var progress = scheduledTalk.SectionProgress
            .FirstOrDefault(p => p.SectionId == request.SectionId);

        if (progress == null)
        {
            progress = new ScheduledTalkSectionProgress
            {
                Id = Guid.NewGuid(),
                ScheduledTalkId = scheduledTalk.Id,
                SectionId = request.SectionId,
                IsRead = false,
                TimeSpentSeconds = 0
            };
            scheduledTalk.SectionProgress.Add(progress);
            _dbContext.ScheduledTalkSectionProgress.Add(progress);
        }

        // Update the progress
        var now = DateTime.UtcNow;
        progress.IsRead = true;
        progress.ReadAt = now;

        if (request.TimeSpentSeconds.HasValue && request.TimeSpentSeconds.Value > 0)
        {
            progress.TimeSpentSeconds += request.TimeSpentSeconds.Value;
        }

        // If this is the first section being read, update status to InProgress
        if (scheduledTalk.Status == ScheduledTalkStatus.Pending)
        {
            scheduledTalk.Status = ScheduledTalkStatus.InProgress;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ScheduledTalkSectionProgressDto
        {
            Id = progress.Id,
            ScheduledTalkId = progress.ScheduledTalkId,
            SectionId = progress.SectionId,
            SectionTitle = section.Title,
            SectionNumber = section.SectionNumber,
            IsRead = progress.IsRead,
            ReadAt = progress.ReadAt,
            TimeSpentSeconds = progress.TimeSpentSeconds
        };
    }
}
