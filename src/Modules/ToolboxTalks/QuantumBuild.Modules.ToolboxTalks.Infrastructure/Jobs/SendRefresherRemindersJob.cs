using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Hangfire background job for sending refresher training reminder emails.
/// Sends reminders at 2 weeks and 1 week before the refresher due date.
/// Runs daily at 9:00 AM.
/// </summary>
public class SendRefresherRemindersJob
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ITenantRepository _tenantRepository;
    private readonly IToolboxTalkEmailService _emailService;
    private readonly ILogger<SendRefresherRemindersJob> _logger;

    public SendRefresherRemindersJob(
        IToolboxTalksDbContext dbContext,
        ITenantRepository tenantRepository,
        IToolboxTalkEmailService emailService,
        ILogger<SendRefresherRemindersJob> logger)
    {
        _dbContext = dbContext;
        _tenantRepository = tenantRepository;
        _emailService = emailService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting SendRefresherRemindersJob");

        var now = DateTime.UtcNow;
        var twoWeeksFromNow = now.AddDays(14);
        var oneWeekFromNow = now.AddDays(7);
        var twoWeekReminders = 0;
        var oneWeekReminders = 0;
        var errorCount = 0;

        var tenants = await _tenantRepository.GetAllActiveAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            try
            {
                // Process standalone talk refreshers - 2 week reminders
                var talks2Weeks = await _dbContext.ScheduledTalks
                    .IgnoreQueryFilters()
                    .Include(st => st.Employee)
                    .Include(st => st.ToolboxTalk)
                    .Where(st => st.TenantId == tenant.Id
                        && !st.IsDeleted
                        && st.IsRefresher
                        && st.Status != ScheduledTalkStatus.Completed
                        && st.Status != ScheduledTalkStatus.Cancelled
                        && !st.ReminderSent2Weeks
                        && st.RefresherDueDate != null
                        && st.RefresherDueDate <= twoWeeksFromNow
                        && st.RefresherDueDate > oneWeekFromNow)
                    .ToListAsync(cancellationToken);

                foreach (var talk in talks2Weeks)
                {
                    try
                    {
                        await _emailService.SendRefresherReminderAsync(talk, talk.Employee, "2 weeks", cancellationToken);
                        talk.ReminderSent2Weeks = true;
                        twoWeekReminders++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending 2-week refresher reminder for talk {TalkId}", talk.Id);
                        errorCount++;
                    }
                }

                // Process standalone talk refreshers - 1 week reminders
                var talks1Week = await _dbContext.ScheduledTalks
                    .IgnoreQueryFilters()
                    .Include(st => st.Employee)
                    .Include(st => st.ToolboxTalk)
                    .Where(st => st.TenantId == tenant.Id
                        && !st.IsDeleted
                        && st.IsRefresher
                        && st.Status != ScheduledTalkStatus.Completed
                        && st.Status != ScheduledTalkStatus.Cancelled
                        && !st.ReminderSent1Week
                        && st.RefresherDueDate != null
                        && st.RefresherDueDate <= oneWeekFromNow)
                    .ToListAsync(cancellationToken);

                foreach (var talk in talks1Week)
                {
                    try
                    {
                        await _emailService.SendRefresherReminderAsync(talk, talk.Employee, "1 week", cancellationToken);
                        talk.ReminderSent1Week = true;
                        oneWeekReminders++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending 1-week refresher reminder for talk {TalkId}", talk.Id);
                        errorCount++;
                    }
                }

                // Process course assignment refreshers - 2 week reminders
                var courseAssignments2Weeks = await _dbContext.ToolboxTalkCourseAssignments
                    .IgnoreQueryFilters()
                    .Include(a => a.Employee)
                    .Include(a => a.Course)
                    .Where(a => a.TenantId == tenant.Id
                        && !a.IsDeleted
                        && a.IsRefresher
                        && a.Status != CourseAssignmentStatus.Completed
                        && !a.ReminderSent2Weeks
                        && a.RefresherDueDate != null
                        && a.RefresherDueDate <= twoWeeksFromNow
                        && a.RefresherDueDate > oneWeekFromNow)
                    .ToListAsync(cancellationToken);

                foreach (var assignment in courseAssignments2Weeks)
                {
                    try
                    {
                        await _emailService.SendCourseRefresherReminderAsync(assignment, assignment.Employee, "2 weeks", cancellationToken);
                        assignment.ReminderSent2Weeks = true;
                        twoWeekReminders++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending 2-week course refresher reminder for assignment {AssignmentId}", assignment.Id);
                        errorCount++;
                    }
                }

                // Process course assignment refreshers - 1 week reminders
                var courseAssignments1Week = await _dbContext.ToolboxTalkCourseAssignments
                    .IgnoreQueryFilters()
                    .Include(a => a.Employee)
                    .Include(a => a.Course)
                    .Where(a => a.TenantId == tenant.Id
                        && !a.IsDeleted
                        && a.IsRefresher
                        && a.Status != CourseAssignmentStatus.Completed
                        && !a.ReminderSent1Week
                        && a.RefresherDueDate != null
                        && a.RefresherDueDate <= oneWeekFromNow)
                    .ToListAsync(cancellationToken);

                foreach (var assignment in courseAssignments1Week)
                {
                    try
                    {
                        await _emailService.SendCourseRefresherReminderAsync(assignment, assignment.Employee, "1 week", cancellationToken);
                        assignment.ReminderSent1Week = true;
                        oneWeekReminders++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending 1-week course refresher reminder for assignment {AssignmentId}", assignment.Id);
                        errorCount++;
                    }
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing refresher reminders for tenant {TenantId}", tenant.Id);
            }
        }

        _logger.LogInformation(
            "Completed SendRefresherRemindersJob. 2-week reminders: {TwoWeek}, 1-week reminders: {OneWeek}, Errors: {Errors}",
            twoWeekReminders, oneWeekReminders, errorCount);
    }
}
