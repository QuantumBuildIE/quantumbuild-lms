using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkDashboard;

public class GetToolboxTalkDashboardQueryHandler : IRequestHandler<GetToolboxTalkDashboardQuery, ToolboxTalkDashboardDto>
{
    private readonly IToolboxTalksDbContext _context;

    public GetToolboxTalkDashboardQueryHandler(IToolboxTalksDbContext context)
    {
        _context = context;
    }

    public async Task<ToolboxTalkDashboardDto> Handle(GetToolboxTalkDashboardQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Get talk counts
        var talks = await _context.ToolboxTalks
            .Where(t => t.TenantId == request.TenantId && !t.IsDeleted)
            .Select(t => new { t.Id, t.IsActive, t.Frequency })
            .ToListAsync(cancellationToken);

        var totalTalks = talks.Count;
        var activeTalks = talks.Count(t => t.IsActive);
        var inactiveTalks = totalTalks - activeTalks;

        // Get all scheduled talks for statistics
        var scheduledTalksQuery = _context.ScheduledTalks
            .Include(st => st.ToolboxTalk)
            .Include(st => st.Employee)
            .Include(st => st.Completion)
            .Where(st => st.TenantId == request.TenantId && !st.IsDeleted);

        if (request.EmployeeIds != null)
        {
            scheduledTalksQuery = scheduledTalksQuery.Where(st => request.EmployeeIds.Contains(st.EmployeeId));
        }

        var scheduledTalks = await scheduledTalksQuery.ToListAsync(cancellationToken);

        // Calculate counts by status
        var totalAssignments = scheduledTalks.Count;
        var pendingCount = scheduledTalks.Count(st => st.Status == ScheduledTalkStatus.Pending);
        var inProgressCount = scheduledTalks.Count(st => st.Status == ScheduledTalkStatus.InProgress);
        var completedCount = scheduledTalks.Count(st => st.Status == ScheduledTalkStatus.Completed);
        var overdueCount = scheduledTalks.Count(st =>
            st.Status == ScheduledTalkStatus.Overdue ||
            (st.Status != ScheduledTalkStatus.Completed && st.Status != ScheduledTalkStatus.Cancelled && st.DueDate < now));

        // Calculate rates
        var completionRate = totalAssignments > 0
            ? Math.Round((decimal)completedCount / totalAssignments * 100, 2)
            : 0;
        var overdueRate = totalAssignments > 0
            ? Math.Round((decimal)overdueCount / totalAssignments * 100, 2)
            : 0;

        // Calculate average completion time
        var completionsWithTime = scheduledTalks
            .Where(st => st.Completion != null && st.Completion.TotalTimeSpentSeconds > 0)
            .Select(st => st.Completion!.TotalTimeSpentSeconds)
            .ToList();
        var avgCompletionTimeHours = completionsWithTime.Count > 0
            ? Math.Round((decimal)completionsWithTime.Average() / 3600m, 2)
            : 0;

        // Calculate quiz statistics
        var quizAttemptsQuery = _context.ScheduledTalkQuizAttempts
            .Where(qa => qa.ScheduledTalk.TenantId == request.TenantId);

        if (request.EmployeeIds != null)
        {
            quizAttemptsQuery = quizAttemptsQuery.Where(qa => request.EmployeeIds.Contains(qa.ScheduledTalk.EmployeeId));
        }

        var quizAttempts = await quizAttemptsQuery.ToListAsync(cancellationToken);

        var averageQuizScore = quizAttempts.Count > 0 && quizAttempts.Any(qa => qa.MaxScore > 0)
            ? Math.Round(quizAttempts.Where(qa => qa.MaxScore > 0).Average(qa => (decimal)qa.Score / qa.MaxScore * 100), 2)
            : 0;
        var quizPassRate = quizAttempts.Count > 0
            ? Math.Round((decimal)quizAttempts.Count(qa => qa.Passed) / quizAttempts.Count * 100, 2)
            : 0;

        // Build status breakdown
        var talksByStatus = new Dictionary<ScheduledTalkStatus, int>
        {
            { ScheduledTalkStatus.Pending, pendingCount },
            { ScheduledTalkStatus.InProgress, inProgressCount },
            { ScheduledTalkStatus.Completed, completedCount },
            { ScheduledTalkStatus.Overdue, overdueCount },
            { ScheduledTalkStatus.Cancelled, scheduledTalks.Count(st => st.Status == ScheduledTalkStatus.Cancelled) }
        };

        // Build frequency breakdown
        var talksByFrequency = talks
            .GroupBy(t => t.Frequency)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get recent completions (last 10)
        var recentCompletions = scheduledTalks
            .Where(st => st.Completion != null)
            .OrderByDescending(st => st.Completion!.CompletedAt)
            .Take(10)
            .Select(st => new RecentCompletionDto
            {
                ScheduledTalkId = st.Id,
                EmployeeName = $"{st.Employee?.FirstName} {st.Employee?.LastName}".Trim(),
                ToolboxTalkCode = st.ToolboxTalk?.Code ?? string.Empty,
                ToolboxTalkTitle = st.ToolboxTalk?.Title ?? "Unknown",
                CompletedAt = st.Completion!.CompletedAt,
                TotalTimeSpentSeconds = st.Completion.TotalTimeSpentSeconds,
                QuizPassed = st.Completion.QuizPassed,
                QuizScore = st.Completion.QuizScore.HasValue && st.Completion.QuizMaxScore.HasValue && st.Completion.QuizMaxScore.Value > 0
                    ? Math.Round((decimal)st.Completion.QuizScore.Value / st.Completion.QuizMaxScore.Value * 100, 2)
                    : null
            })
            .ToList();

        // Get overdue assignments (top 10)
        var overdueAssignments = scheduledTalks
            .Where(st => (st.Status == ScheduledTalkStatus.Overdue ||
                         (st.Status != ScheduledTalkStatus.Completed && st.Status != ScheduledTalkStatus.Cancelled && st.DueDate < now)))
            .OrderBy(st => st.DueDate)
            .Take(10)
            .Select(st => new OverdueAssignmentDto
            {
                ScheduledTalkId = st.Id,
                EmployeeId = st.EmployeeId,
                EmployeeName = $"{st.Employee?.FirstName} {st.Employee?.LastName}".Trim(),
                EmployeeEmail = st.Employee?.Email,
                ToolboxTalkCode = st.ToolboxTalk?.Code ?? string.Empty,
                ToolboxTalkTitle = st.ToolboxTalk?.Title ?? "Unknown",
                DueDate = st.DueDate,
                DaysOverdue = (int)Math.Floor((now - st.DueDate).TotalDays),
                RemindersSent = st.RemindersSent,
                Status = st.Status
            })
            .ToList();

        // Get upcoming schedules (next 10)
        var upcomingSchedules = await _context.ToolboxTalkSchedules
            .Include(s => s.ToolboxTalk)
            .Include(s => s.Assignments)
            .Where(s => s.TenantId == request.TenantId &&
                       !s.IsDeleted &&
                       s.Status == ToolboxTalkScheduleStatus.Active &&
                       (s.NextRunDate.HasValue && s.NextRunDate >= now))
            .OrderBy(s => s.NextRunDate)
            .Take(10)
            .Select(s => new UpcomingScheduleDto
            {
                ScheduleId = s.Id,
                ToolboxTalkCode = s.ToolboxTalk.Code,
                ToolboxTalkTitle = s.ToolboxTalk.Title,
                ScheduledDate = s.NextRunDate ?? s.ScheduledDate,
                Frequency = s.Frequency,
                FrequencyDisplay = GetFrequencyDisplay(s.Frequency),
                AssignmentCount = s.Assignments.Count,
                AssignToAllEmployees = s.AssignToAllEmployees
            })
            .ToListAsync(cancellationToken);

        return new ToolboxTalkDashboardDto
        {
            TotalTalks = totalTalks,
            ActiveTalks = activeTalks,
            InactiveTalks = inactiveTalks,
            TotalAssignments = totalAssignments,
            PendingCount = pendingCount,
            InProgressCount = inProgressCount,
            CompletedCount = completedCount,
            OverdueCount = overdueCount,
            CompletionRate = completionRate,
            OverdueRate = overdueRate,
            AverageCompletionTimeHours = avgCompletionTimeHours,
            AverageQuizScore = averageQuizScore,
            QuizPassRate = quizPassRate,
            TalksByStatus = talksByStatus,
            TalksByFrequency = talksByFrequency,
            RecentCompletions = recentCompletions,
            OverdueAssignments = overdueAssignments,
            UpcomingSchedules = upcomingSchedules
        };
    }

    private static string GetFrequencyDisplay(ToolboxTalkFrequency frequency) => frequency switch
    {
        ToolboxTalkFrequency.Once => "One-time",
        ToolboxTalkFrequency.Weekly => "Weekly",
        ToolboxTalkFrequency.Monthly => "Monthly",
        ToolboxTalkFrequency.Annually => "Annually",
        _ => frequency.ToString()
    };
}
