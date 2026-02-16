using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

public class RefresherSchedulingService(
    IToolboxTalksDbContext context,
    ILogger<RefresherSchedulingService> logger) : IRefresherSchedulingService
{
    public async Task ScheduleRefresherIfRequired(ScheduledTalk completedTalk, CancellationToken ct = default)
    {
        // Get the toolbox talk to check refresher settings
        var talk = await context.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == completedTalk.ToolboxTalkId, ct);

        if (talk == null || !talk.RequiresRefresher)
            return;

        // Check if a pending refresher already exists for this employee/talk
        var existingRefresher = await context.ScheduledTalks
            .AnyAsync(st => st.EmployeeId == completedTalk.EmployeeId
                && st.ToolboxTalkId == completedTalk.ToolboxTalkId
                && st.IsRefresher
                && st.Status != ScheduledTalkStatus.Completed
                && st.Status != ScheduledTalkStatus.Cancelled
                && !st.IsDeleted, ct);

        if (existingRefresher)
        {
            logger.LogInformation("Refresher already scheduled for employee {EmployeeId}, talk {TalkId}",
                completedTalk.EmployeeId, completedTalk.ToolboxTalkId);
            return;
        }

        // Calculate refresher due date from completion time
        var completedAt = completedTalk.Completion?.CompletedAt ?? DateTime.UtcNow;
        var refresherDueDate = completedAt.AddMonths(talk.RefresherIntervalMonths);

        var refresher = new ScheduledTalk
        {
            Id = Guid.NewGuid(),
            TenantId = completedTalk.TenantId,
            ToolboxTalkId = completedTalk.ToolboxTalkId,
            EmployeeId = completedTalk.EmployeeId,
            RequiredDate = refresherDueDate.AddDays(-14), // Make available 2 weeks before due
            DueDate = refresherDueDate,
            Status = ScheduledTalkStatus.Pending,
            IsRefresher = true,
            OriginalScheduledTalkId = completedTalk.Id,
            RefresherDueDate = refresherDueDate,
        };

        context.ScheduledTalks.Add(refresher);
        var saved = await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Scheduled refresher for employee {EmployeeId}, talk {TalkId}, due {DueDate} ({Rows} rows saved)",
            completedTalk.EmployeeId, completedTalk.ToolboxTalkId, refresherDueDate, saved);
    }

    public async Task ScheduleRefresherIfRequired(ToolboxTalkCourseAssignment completedAssignment, CancellationToken ct = default)
    {
        // Get the course to check refresher settings
        var course = await context.ToolboxTalkCourses
            .Include(c => c.CourseItems.Where(ci => !ci.IsDeleted))
            .FirstOrDefaultAsync(c => c.Id == completedAssignment.CourseId, ct);

        if (course == null || !course.RequiresRefresher)
            return;

        // Check if a pending refresher already exists
        var existingRefresher = await context.ToolboxTalkCourseAssignments
            .AnyAsync(a => a.EmployeeId == completedAssignment.EmployeeId
                && a.CourseId == completedAssignment.CourseId
                && a.IsRefresher
                && a.Status != CourseAssignmentStatus.Completed
                && !a.IsDeleted, ct);

        if (existingRefresher)
        {
            logger.LogInformation("Refresher already scheduled for employee {EmployeeId}, course {CourseId}",
                completedAssignment.EmployeeId, completedAssignment.CourseId);
            return;
        }

        // Calculate refresher due date
        var refresherDueDate = (completedAssignment.CompletedAt ?? DateTime.UtcNow)
            .AddMonths(course.RefresherIntervalMonths);

        // Create the course refresher assignment
        var refresherAssignment = new ToolboxTalkCourseAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = completedAssignment.TenantId,
            CourseId = completedAssignment.CourseId,
            EmployeeId = completedAssignment.EmployeeId,
            AssignedAt = DateTime.UtcNow,
            DueDate = refresherDueDate,
            Status = CourseAssignmentStatus.Assigned,
            IsRefresher = true,
            OriginalAssignmentId = completedAssignment.Id,
            RefresherDueDate = refresherDueDate,
        };

        // Create ScheduledTalks for each course item
        var courseItems = course.CourseItems.OrderBy(ci => ci.OrderIndex).ToList();
        foreach (var item in courseItems)
        {
            var scheduledTalk = new ScheduledTalk
            {
                Id = Guid.NewGuid(),
                TenantId = completedAssignment.TenantId,
                ToolboxTalkId = item.ToolboxTalkId,
                EmployeeId = completedAssignment.EmployeeId,
                RequiredDate = refresherDueDate.AddDays(-14),
                DueDate = refresherDueDate,
                Status = ScheduledTalkStatus.Pending,
                CourseAssignmentId = refresherAssignment.Id,
                CourseOrderIndex = item.OrderIndex,
                IsRefresher = true,
            };
            context.ScheduledTalks.Add(scheduledTalk);
        }

        context.ToolboxTalkCourseAssignments.Add(refresherAssignment);
        var saved = await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Scheduled course refresher for employee {EmployeeId}, course {CourseId}, due {DueDate} ({Rows} rows saved)",
            completedAssignment.EmployeeId, completedAssignment.CourseId, refresherDueDate, saved);
    }
}
