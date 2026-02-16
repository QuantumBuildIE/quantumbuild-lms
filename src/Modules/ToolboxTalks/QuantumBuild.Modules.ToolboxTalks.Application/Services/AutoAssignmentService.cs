using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Automatically assigns training (courses and standalone talks) to new employees
/// based on auto-assign configuration on courses and talks.
/// </summary>
public class AutoAssignmentService : INewEmployeeTrainingAssigner
{
    private readonly IToolboxTalksDbContext _context;
    private readonly ILogger<AutoAssignmentService> _logger;

    public AutoAssignmentService(
        IToolboxTalksDbContext context,
        ILogger<AutoAssignmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AssignNewEmployeeTrainingAsync(
        Guid tenantId,
        Guid employeeId,
        DateTime? startDate = null,
        CancellationToken ct = default)
    {
        var hireDate = startDate ?? DateTime.UtcNow;

        _logger.LogInformation(
            "Auto-assigning training for new employee {EmployeeId} in tenant {TenantId}, hire date {HireDate}",
            employeeId, tenantId, hireDate);

        // Find auto-assign courses
        var courses = await _context.ToolboxTalkCourses
            .Include(c => c.CourseItems.Where(ci => !ci.IsDeleted))
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId
                && c.AutoAssignToNewEmployees
                && c.IsActive
                && !c.IsDeleted)
            .ToListAsync(ct);

        var courseTalkIds = new HashSet<Guid>();

        foreach (var course in courses)
        {
            var dueDate = hireDate.AddDays(course.AutoAssignDueDays);

            // Check if already assigned
            var alreadyAssigned = await _context.ToolboxTalkCourseAssignments
                .IgnoreQueryFilters()
                .AnyAsync(a => a.CourseId == course.Id
                    && a.EmployeeId == employeeId
                    && a.TenantId == tenantId
                    && !a.IsDeleted, ct);

            if (alreadyAssigned) continue;

            var assignment = new ToolboxTalkCourseAssignment
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CourseId = course.Id,
                EmployeeId = employeeId,
                AssignedAt = DateTime.UtcNow,
                DueDate = dueDate,
                Status = CourseAssignmentStatus.Assigned,
            };

            // Create scheduled talks for each course item
            foreach (var item in course.CourseItems.OrderBy(ci => ci.OrderIndex))
            {
                courseTalkIds.Add(item.ToolboxTalkId);

                var scheduledTalk = new ScheduledTalk
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ToolboxTalkId = item.ToolboxTalkId,
                    EmployeeId = employeeId,
                    RequiredDate = DateTime.UtcNow,
                    DueDate = dueDate,
                    Status = ScheduledTalkStatus.Pending,
                    CourseAssignmentId = assignment.Id,
                    CourseOrderIndex = item.OrderIndex,
                };
                assignment.ScheduledTalks.Add(scheduledTalk);
            }

            _context.ToolboxTalkCourseAssignments.Add(assignment);

            _logger.LogInformation(
                "Auto-assigned course {CourseId} ({CourseTitle}) to employee {EmployeeId}, due {DueDate}, {TalkCount} talks",
                course.Id, course.Title, employeeId, dueDate, assignment.ScheduledTalks.Count);
        }

        // Find auto-assign standalone talks (not already covered by auto-assign courses)
        var standaloneTalks = await _context.ToolboxTalks
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId
                && t.AutoAssignToNewEmployees
                && t.Status == ToolboxTalkStatus.Published
                && !t.IsDeleted
                && !courseTalkIds.Contains(t.Id))
            .ToListAsync(ct);

        foreach (var talk in standaloneTalks)
        {
            var dueDate = hireDate.AddDays(talk.AutoAssignDueDays);

            var alreadyAssigned = await _context.ScheduledTalks
                .IgnoreQueryFilters()
                .AnyAsync(st => st.ToolboxTalkId == talk.Id
                    && st.EmployeeId == employeeId
                    && st.TenantId == tenantId
                    && !st.IsDeleted, ct);

            if (alreadyAssigned) continue;

            var scheduledTalk = new ScheduledTalk
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ToolboxTalkId = talk.Id,
                EmployeeId = employeeId,
                RequiredDate = DateTime.UtcNow,
                DueDate = dueDate,
                Status = ScheduledTalkStatus.Pending,
            };

            _context.ScheduledTalks.Add(scheduledTalk);

            _logger.LogInformation(
                "Auto-assigned standalone talk {TalkId} ({TalkTitle}) to employee {EmployeeId}, due {DueDate}",
                talk.Id, talk.Title, employeeId, dueDate);
        }

        var savedCount = await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Auto-assignment complete for employee {EmployeeId}: {CourseCount} courses, {TalkCount} standalone talks, {SavedCount} rows saved",
            employeeId, courses.Count, standaloneTalks.Count, savedCount);
    }
}
