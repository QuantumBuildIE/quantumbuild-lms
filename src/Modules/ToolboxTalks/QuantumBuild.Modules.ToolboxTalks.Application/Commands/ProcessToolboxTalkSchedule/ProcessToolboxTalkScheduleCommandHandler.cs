using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.ProcessToolboxTalkSchedule;

public class ProcessToolboxTalkScheduleCommandHandler : IRequestHandler<ProcessToolboxTalkScheduleCommand, ProcessToolboxTalkScheduleResult>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;
    private readonly IToolboxTalkEmailService _emailService;
    private readonly ILogger<ProcessToolboxTalkScheduleCommandHandler> _logger;

    public ProcessToolboxTalkScheduleCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext,
        IToolboxTalkEmailService emailService,
        ILogger<ProcessToolboxTalkScheduleCommandHandler> logger)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<ProcessToolboxTalkScheduleResult> Handle(ProcessToolboxTalkScheduleCommand request, CancellationToken cancellationToken)
    {
        // Get the schedule with assignments and toolbox talk sections
        var schedule = await _dbContext.ToolboxTalkSchedules
            .Include(s => s.Assignments)
            .Include(s => s.ToolboxTalk)
                .ThenInclude(t => t.Sections)
            .FirstOrDefaultAsync(s => s.Id == request.ScheduleId && s.TenantId == request.TenantId, cancellationToken);

        if (schedule == null)
        {
            throw new InvalidOperationException($"Schedule with ID '{request.ScheduleId}' not found.");
        }

        // Validate schedule can be processed
        if (schedule.Status == ToolboxTalkScheduleStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot process a cancelled schedule.");
        }

        if (schedule.Status == ToolboxTalkScheduleStatus.Completed)
        {
            throw new InvalidOperationException("Schedule has already been completed.");
        }

        // Get tenant settings for default due days
        var settings = await _dbContext.ToolboxTalkSettings
            .FirstOrDefaultAsync(s => s.TenantId == request.TenantId, cancellationToken);

        var defaultDueDays = settings?.DefaultDueDays ?? 7;

        // Get employee data for language code assignment and email sending
        var employeeIds = schedule.Assignments.Select(a => a.EmployeeId).ToList();
        var employees = await _coreDbContext.Employees
            .Where(e => e.TenantId == request.TenantId && !e.IsDeleted && employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e, cancellationToken);

        // Get unprocessed assignments
        var unprocessedAssignments = schedule.Assignments
            .Where(a => !a.IsProcessed)
            .ToList();

        if (!unprocessedAssignments.Any())
        {
            // If AssignToAllEmployees and recurring, refresh assignments
            if (schedule.AssignToAllEmployees && schedule.Frequency != ToolboxTalkFrequency.Once)
            {
                await RefreshAssignmentsForAllEmployees(schedule, request.TenantId, cancellationToken);
                unprocessedAssignments = schedule.Assignments.Where(a => !a.IsProcessed).ToList();
            }
            // If targeted by department/location and recurring, refresh the criteria-derived assignments
            // (explicitly-added employees are left untouched — see RefreshAssignmentsForTargetCriteria)
            else if (!schedule.AssignToAllEmployees
                && (schedule.TargetDepartmentIds.Any() || schedule.TargetSiteIds.Any())
                && schedule.Frequency != ToolboxTalkFrequency.Once)
            {
                await RefreshAssignmentsForTargetCriteria(schedule, request.TenantId, cancellationToken);
                unprocessedAssignments = schedule.Assignments.Where(a => !a.IsProcessed).ToList();
            }
        }

        var talksCreated = 0;
        var now = DateTime.UtcNow;

        // Process each unprocessed assignment
        foreach (var assignment in unprocessedAssignments)
        {
            // Create ScheduledTalk record
            var scheduledTalk = new ScheduledTalk
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                ToolboxTalkId = schedule.ToolboxTalkId,
                EmployeeId = assignment.EmployeeId,
                ScheduleId = schedule.Id,
                RequiredDate = now,
                DueDate = now.AddDays(defaultDueDays),
                Status = ScheduledTalkStatus.Pending,
                RemindersSent = 0,
                LastReminderAt = null,
                LanguageCode = employees.TryGetValue(assignment.EmployeeId, out var emp) ? emp.PreferredLanguage ?? "en" : "en"
            };

            // Create ScheduledTalkSectionProgress records for each section
            foreach (var section in schedule.ToolboxTalk.Sections)
            {
                var sectionProgress = new ScheduledTalkSectionProgress
                {
                    Id = Guid.NewGuid(),
                    ScheduledTalkId = scheduledTalk.Id,
                    SectionId = section.Id,
                    IsRead = false,
                    ReadAt = null,
                    TimeSpentSeconds = 0
                };
                scheduledTalk.SectionProgress.Add(sectionProgress);
            }

            _dbContext.ScheduledTalks.Add(scheduledTalk);

            // Mark assignment as processed
            assignment.IsProcessed = true;
            assignment.ProcessedAt = now;

            talksCreated++;

            // Send assignment notification email to the employee
            if (employees.TryGetValue(assignment.EmployeeId, out var employee))
            {
                // Set the ToolboxTalk reference for email template
                scheduledTalk.ToolboxTalk = schedule.ToolboxTalk;

                try
                {
                    await _emailService.SendTalkAssignmentEmailAsync(scheduledTalk, employee, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send assignment email for ScheduledTalk {TalkId} to Employee {EmployeeId}",
                        scheduledTalk.Id, employee.Id);
                    // Continue processing - don't fail the entire operation due to email failure
                }
            }
        }

        // Handle schedule status and recurring logic
        var scheduleCompleted = false;
        DateTime? nextRunDate = null;

        if (schedule.Frequency == ToolboxTalkFrequency.Once)
        {
            // One-time schedule is now completed
            schedule.Status = ToolboxTalkScheduleStatus.Completed;
            schedule.NextRunDate = null;
            scheduleCompleted = true;
        }
        else
        {
            // Set to Active if it was in Draft
            if (schedule.Status == ToolboxTalkScheduleStatus.Draft)
            {
                schedule.Status = ToolboxTalkScheduleStatus.Active;
            }

            // Calculate next run date based on frequency
            nextRunDate = CalculateNextRunDate(now, schedule.Frequency);

            // Check if next run date is beyond end date
            if (schedule.EndDate.HasValue && nextRunDate > schedule.EndDate.Value)
            {
                schedule.Status = ToolboxTalkScheduleStatus.Completed;
                schedule.NextRunDate = null;
                scheduleCompleted = true;
            }
            else
            {
                schedule.NextRunDate = nextRunDate;

                // Reset assignments for next cycle (mark all as unprocessed)
                foreach (var assignment in schedule.Assignments)
                {
                    assignment.IsProcessed = false;
                    assignment.ProcessedAt = null;
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ProcessToolboxTalkScheduleResult
        {
            TalksCreated = talksCreated,
            ScheduleCompleted = scheduleCompleted,
            NextRunDate = nextRunDate
        };
    }

    private async Task RefreshAssignmentsForAllEmployees(
        ToolboxTalkSchedule schedule,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Get all active employees
        var activeEmployeeIds = await _coreDbContext.Employees
            .Where(e => e.TenantId == tenantId && e.IsActive && !e.IsDeleted)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var currentEmployeeIds = schedule.Assignments.Select(a => a.EmployeeId).ToHashSet();

        // Add new employees
        foreach (var employeeId in activeEmployeeIds)
        {
            if (!currentEmployeeIds.Contains(employeeId))
            {
                var assignment = new ToolboxTalkScheduleAssignment
                {
                    Id = Guid.NewGuid(),
                    ScheduleId = schedule.Id,
                    EmployeeId = employeeId,
                    IsProcessed = false,
                    ProcessedAt = null
                };
                schedule.Assignments.Add(assignment);
            }
        }

        // Remove inactive employees (mark as processed so they don't get talks)
        var inactiveEmployeeIds = currentEmployeeIds.Except(activeEmployeeIds).ToHashSet();
        var inactiveAssignments = schedule.Assignments
            .Where(a => inactiveEmployeeIds.Contains(a.EmployeeId))
            .ToList();

        // Physical delete via ExecuteDeleteAsync — DbSet.Remove() would be soft-deleted by the
        // SetAuditFields interceptor. Required-FK nav-collection Remove() also marks the entity
        // as Deleted via EF orphan-removal semantics, so we Detach explicitly to suppress the
        // phantom soft-delete UPDATE that would otherwise hit zero rows.
        var inactiveAssignmentIds = new List<Guid>();
        foreach (var assignment in inactiveAssignments)
        {
            schedule.Assignments.Remove(assignment); // load-bearing for nav-collection consumers
            _dbContext.Entry(assignment).State = EntityState.Detached;
            inactiveAssignmentIds.Add(assignment.Id);
        }

        if (inactiveAssignmentIds.Count > 0)
        {
            await _dbContext.ToolboxTalkScheduleAssignments
                .Where(a => inactiveAssignmentIds.Contains(a.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Re-derives the department/location-targeted portion of a recurring schedule's assignments.
    /// Only touches assignments flagged IsCriteriaDerived — explicitly-added employees (EmployeeIds
    /// at creation/edit time) are never added or removed by this refresh, mirroring how
    /// RefreshAssignmentsForAllEmployees never coexists with an explicit list (AssignToAllEmployees
    /// and EmployeeIds are mutually exclusive there).
    /// </summary>
    private async Task RefreshAssignmentsForTargetCriteria(
        ToolboxTalkSchedule schedule,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var targetDepartmentIds = schedule.TargetDepartmentIds;
        var targetSiteIds = schedule.TargetSiteIds;

        var currentTargetEmployeeIds = (await _coreDbContext.Employees
            .Where(e => e.TenantId == tenantId && e.IsActive && !e.IsDeleted
                && ((e.DepartmentId.HasValue && targetDepartmentIds.Contains(e.DepartmentId.Value))
                    || (e.PrimarySiteId.HasValue && targetSiteIds.Contains(e.PrimarySiteId.Value))))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)).ToHashSet();

        var existingEmployeeIds = schedule.Assignments.Select(a => a.EmployeeId).ToHashSet();
        var existingCriteriaDerivedEmployeeIds = schedule.Assignments
            .Where(a => a.IsCriteriaDerived)
            .Select(a => a.EmployeeId)
            .ToHashSet();

        // Add newly-qualifying employees (skip anyone already present, whether explicit or criteria-derived)
        foreach (var employeeId in currentTargetEmployeeIds)
        {
            if (!existingEmployeeIds.Contains(employeeId))
            {
                var assignment = new ToolboxTalkScheduleAssignment
                {
                    Id = Guid.NewGuid(),
                    ScheduleId = schedule.Id,
                    EmployeeId = employeeId,
                    IsProcessed = false,
                    ProcessedAt = null,
                    IsCriteriaDerived = true
                };
                schedule.Assignments.Add(assignment);
            }
        }

        // Remove criteria-derived assignments that no longer qualify (department/site changed, or employee
        // went inactive). Explicit (non-criteria-derived) assignments are never removed by this refresh.
        var noLongerQualifying = existingCriteriaDerivedEmployeeIds.Except(currentTargetEmployeeIds).ToHashSet();
        var assignmentsToRemove = schedule.Assignments
            .Where(a => a.IsCriteriaDerived && noLongerQualifying.Contains(a.EmployeeId))
            .ToList();

        var assignmentIdsToRemove = new List<Guid>();
        foreach (var assignment in assignmentsToRemove)
        {
            schedule.Assignments.Remove(assignment); // load-bearing for nav-collection consumers
            _dbContext.Entry(assignment).State = EntityState.Detached;
            assignmentIdsToRemove.Add(assignment.Id);
        }

        if (assignmentIdsToRemove.Count > 0)
        {
            await _dbContext.ToolboxTalkScheduleAssignments
                .Where(a => assignmentIdsToRemove.Contains(a.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    private static DateTime CalculateNextRunDate(DateTime currentDate, ToolboxTalkFrequency frequency)
    {
        return frequency switch
        {
            ToolboxTalkFrequency.Weekly => currentDate.AddDays(7),
            ToolboxTalkFrequency.Monthly => currentDate.AddMonths(1),
            ToolboxTalkFrequency.Annually => currentDate.AddYears(1),
            _ => currentDate // Should not happen for recurring schedules
        };
    }
}
