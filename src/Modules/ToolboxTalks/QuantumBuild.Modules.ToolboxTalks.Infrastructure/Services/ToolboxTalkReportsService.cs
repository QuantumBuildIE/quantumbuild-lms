using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reports;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Implementation of toolbox talk reports service
/// </summary>
public class ToolboxTalkReportsService : IToolboxTalkReportsService
{
    private readonly IToolboxTalksDbContext _context;
    private readonly ICoreDbContext _coreContext;
    private readonly ILogger<ToolboxTalkReportsService> _logger;

    public ToolboxTalkReportsService(
        IToolboxTalksDbContext context,
        ICoreDbContext coreContext,
        ILogger<ToolboxTalkReportsService> logger)
    {
        _context = context;
        _coreContext = coreContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ComplianceReportDto> GetComplianceReportAsync(
        Guid tenantId,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        Guid? siteId = null,
        List<Guid>? employeeIds = null)
    {
        try
        {
            // Normalize dates to UTC
            var utcDateFrom = dateFrom?.ToUniversalTime();
            var utcDateTo = dateTo?.ToUniversalTime();

            // Get all employees in scope
            var employeesQuery = _coreContext.Employees
                .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive);

            if (siteId.HasValue)
            {
                employeesQuery = employeesQuery.Where(e => e.PrimarySiteId == siteId.Value);
            }

            if (employeeIds != null)
            {
                employeesQuery = employeesQuery.Where(e => employeeIds.Contains(e.Id));
            }

            var totalEmployees = await employeesQuery.CountAsync();

            // Build scheduled talks query
            var scheduledTalksQuery = _context.ScheduledTalks
                .Where(st => st.TenantId == tenantId);

            if (utcDateFrom.HasValue)
            {
                scheduledTalksQuery = scheduledTalksQuery.Where(st => st.RequiredDate >= utcDateFrom.Value);
            }

            if (utcDateTo.HasValue)
            {
                scheduledTalksQuery = scheduledTalksQuery.Where(st => st.RequiredDate <= utcDateTo.Value);
            }

            // Filter by employee IDs (supervisor scoping)
            if (employeeIds != null)
            {
                scheduledTalksQuery = scheduledTalksQuery.Where(st => employeeIds.Contains(st.EmployeeId));
            }

            // If site filter, join with employees
            if (siteId.HasValue)
            {
                var employeeIdsInSite = await employeesQuery.Select(e => e.Id).ToListAsync();
                scheduledTalksQuery = scheduledTalksQuery.Where(st => employeeIdsInSite.Contains(st.EmployeeId));
            }

            // Get scheduled talks with completions
            var scheduledTalks = await scheduledTalksQuery
                .Include(st => st.ToolboxTalk)
                .Include(st => st.Completion)
                .Include(st => st.Employee)
                .ToListAsync();

            var assignedCount = scheduledTalks.Count;
            var completedCount = scheduledTalks.Count(st => st.Status == ScheduledTalkStatus.Completed);
            var overdueCount = scheduledTalks.Count(st => st.Status == ScheduledTalkStatus.Overdue ||
                (st.Status != ScheduledTalkStatus.Completed && st.Status != ScheduledTalkStatus.Cancelled && st.DueDate < DateTime.UtcNow));
            var pendingCount = scheduledTalks.Count(st => st.Status == ScheduledTalkStatus.Pending);
            var inProgressCount = scheduledTalks.Count(st => st.Status == ScheduledTalkStatus.InProgress);

            var compliancePercentage = assignedCount > 0
                ? Math.Round((decimal)completedCount / assignedCount * 100, 2)
                : 0;

            // Get breakdown by site/department
            var byDepartment = new List<DepartmentComplianceDto>();
            var sites = await _coreContext.Sites
                .Where(s => s.TenantId == tenantId && !s.IsDeleted)
                .ToListAsync();

            foreach (var site in sites)
            {
                var siteEmployeeIds = await _coreContext.Employees
                    .Where(e => e.TenantId == tenantId && !e.IsDeleted && e.IsActive && e.PrimarySiteId == site.Id)
                    .Select(e => e.Id)
                    .ToListAsync();

                var siteTalks = scheduledTalks.Where(st => siteEmployeeIds.Contains(st.EmployeeId)).ToList();

                if (siteTalks.Any())
                {
                    var siteCompleted = siteTalks.Count(st => st.Status == ScheduledTalkStatus.Completed);
                    var siteOverdue = siteTalks.Count(st => st.Status == ScheduledTalkStatus.Overdue ||
                        (st.Status != ScheduledTalkStatus.Completed && st.Status != ScheduledTalkStatus.Cancelled && st.DueDate < DateTime.UtcNow));

                    byDepartment.Add(new DepartmentComplianceDto
                    {
                        SiteId = site.Id,
                        DepartmentName = site.SiteName,
                        TotalEmployees = siteEmployeeIds.Count,
                        AssignedCount = siteTalks.Count,
                        CompletedCount = siteCompleted,
                        CompliancePercentage = siteTalks.Count > 0
                            ? Math.Round((decimal)siteCompleted / siteTalks.Count * 100, 2)
                            : 0,
                        OverdueCount = siteOverdue
                    });
                }
            }

            // Get breakdown by toolbox talk
            var byTalk = new List<TalkComplianceDto>();
            var talkGroups = scheduledTalks
                .GroupBy(st => new { st.ToolboxTalkId, st.ToolboxTalk.Title })
                .ToList();

            foreach (var group in talkGroups)
            {
                var talkTalks = group.ToList();
                var talkCompleted = talkTalks.Count(st => st.Status == ScheduledTalkStatus.Completed);
                var talkOverdue = talkTalks.Count(st => st.Status == ScheduledTalkStatus.Overdue ||
                    (st.Status != ScheduledTalkStatus.Completed && st.Status != ScheduledTalkStatus.Cancelled && st.DueDate < DateTime.UtcNow));

                // Calculate quiz metrics
                var completionsWithQuiz = talkTalks
                    .Where(st => st.Completion?.QuizScore != null && st.Completion?.QuizMaxScore != null)
                    .Select(st => st.Completion)
                    .ToList();

                decimal? avgQuizScore = null;
                decimal? quizPassRate = null;

                if (completionsWithQuiz.Any())
                {
                    avgQuizScore = Math.Round(
                        completionsWithQuiz.Average(c => c!.QuizMaxScore > 0
                            ? (decimal)c.QuizScore!.Value / c.QuizMaxScore!.Value * 100
                            : 0), 2);
                    quizPassRate = Math.Round(
                        (decimal)completionsWithQuiz.Count(c => c!.QuizPassed == true) / completionsWithQuiz.Count * 100, 2);
                }

                byTalk.Add(new TalkComplianceDto
                {
                    ToolboxTalkId = group.Key.ToolboxTalkId,
                    Title = group.Key.Title,
                    AssignedCount = talkTalks.Count,
                    CompletedCount = talkCompleted,
                    CompliancePercentage = talkTalks.Count > 0
                        ? Math.Round((decimal)talkCompleted / talkTalks.Count * 100, 2)
                        : 0,
                    OverdueCount = talkOverdue,
                    AverageQuizScore = avgQuizScore,
                    QuizPassRate = quizPassRate
                });
            }

            return new ComplianceReportDto
            {
                TotalEmployees = totalEmployees,
                AssignedCount = assignedCount,
                CompletedCount = completedCount,
                CompliancePercentage = compliancePercentage,
                OverdueCount = overdueCount,
                PendingCount = pendingCount,
                InProgressCount = inProgressCount,
                ByDepartment = byDepartment.OrderByDescending(d => d.CompliancePercentage).ToList(),
                ByTalk = byTalk.OrderByDescending(t => t.AssignedCount).ToList(),
                DateFrom = dateFrom,
                DateTo = dateTo,
                GeneratedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report for tenant {TenantId}", tenantId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<OverdueItemDto>> GetOverdueReportAsync(
        Guid tenantId,
        Guid? siteId = null,
        Guid? toolboxTalkId = null,
        List<Guid>? employeeIds = null)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Build query for overdue items
            var query = _context.ScheduledTalks
                .Where(st => st.TenantId == tenantId)
                .Where(st => st.Status == ScheduledTalkStatus.Overdue ||
                    (st.Status != ScheduledTalkStatus.Completed &&
                     st.Status != ScheduledTalkStatus.Cancelled &&
                     st.DueDate < now))
                .Include(st => st.ToolboxTalk)
                .Include(st => st.Employee)
                    .ThenInclude(e => e.PrimarySite)
                .AsQueryable();

            if (toolboxTalkId.HasValue)
            {
                query = query.Where(st => st.ToolboxTalkId == toolboxTalkId.Value);
            }

            if (siteId.HasValue)
            {
                query = query.Where(st => st.Employee.PrimarySiteId == siteId.Value);
            }

            if (employeeIds != null)
            {
                query = query.Where(st => employeeIds.Contains(st.EmployeeId));
            }

            var overdueItems = await query
                .OrderByDescending(st => (now - st.DueDate).TotalDays)
                .ToListAsync();

            return overdueItems.Select(st => new OverdueItemDto
            {
                ScheduledTalkId = st.Id,
                EmployeeId = st.EmployeeId,
                EmployeeName = $"{st.Employee.FirstName} {st.Employee.LastName}",
                Email = st.Employee.Email,
                SiteName = st.Employee.PrimarySite?.SiteName,
                ToolboxTalkId = st.ToolboxTalkId,
                TalkTitle = st.ToolboxTalk.Title,
                DueDate = st.DueDate,
                DaysOverdue = (int)Math.Ceiling((now - st.DueDate).TotalDays),
                RemindersSent = st.RemindersSent,
                LastReminderAt = st.LastReminderAt,
                IsInProgress = st.Status == ScheduledTalkStatus.InProgress,
                VideoWatchPercent = st.VideoWatchPercent
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating overdue report for tenant {TenantId}", tenantId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<PaginatedList<CompletionDetailDto>> GetCompletionReportAsync(
        Guid tenantId,
        DateTime? dateFrom = null,
        DateTime? dateTo = null,
        Guid? toolboxTalkId = null,
        Guid? siteId = null,
        int pageNumber = 1,
        int pageSize = 20,
        List<Guid>? employeeIds = null)
    {
        try
        {
            // Normalize dates to UTC
            var utcDateFrom = dateFrom?.ToUniversalTime();
            var utcDateTo = dateTo?.ToUniversalTime();

            var query = _context.ScheduledTalkCompletions
                .Include(c => c.ScheduledTalk)
                    .ThenInclude(st => st.ToolboxTalk)
                .Include(c => c.ScheduledTalk)
                    .ThenInclude(st => st.Employee)
                        .ThenInclude(e => e.PrimarySite)
                .Where(c => c.ScheduledTalk.TenantId == tenantId)
                .AsQueryable();

            if (utcDateFrom.HasValue)
            {
                query = query.Where(c => c.CompletedAt >= utcDateFrom.Value);
            }

            if (utcDateTo.HasValue)
            {
                query = query.Where(c => c.CompletedAt <= utcDateTo.Value);
            }

            if (toolboxTalkId.HasValue)
            {
                query = query.Where(c => c.ScheduledTalk.ToolboxTalkId == toolboxTalkId.Value);
            }

            if (siteId.HasValue)
            {
                query = query.Where(c => c.ScheduledTalk.Employee.PrimarySiteId == siteId.Value);
            }

            if (employeeIds != null)
            {
                query = query.Where(c => employeeIds.Contains(c.ScheduledTalk.EmployeeId));
            }

            var totalCount = await query.CountAsync();

            var completions = await query
                .OrderByDescending(c => c.CompletedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = completions.Select(c => new CompletionDetailDto
            {
                ScheduledTalkId = c.ScheduledTalkId,
                CompletionId = c.Id,
                EmployeeId = c.ScheduledTalk.EmployeeId,
                EmployeeName = $"{c.ScheduledTalk.Employee.FirstName} {c.ScheduledTalk.Employee.LastName}",
                Email = c.ScheduledTalk.Employee.Email,
                SiteName = c.ScheduledTalk.Employee.PrimarySite?.SiteName,
                ToolboxTalkId = c.ScheduledTalk.ToolboxTalkId,
                TalkTitle = c.ScheduledTalk.ToolboxTalk.Title,
                RequiredDate = c.ScheduledTalk.RequiredDate,
                DueDate = c.ScheduledTalk.DueDate,
                CompletedAt = c.CompletedAt,
                TimeSpentMinutes = c.TotalTimeSpentSeconds / 60,
                VideoWatchPercent = c.VideoWatchPercent,
                QuizScore = c.QuizScore,
                QuizMaxScore = c.QuizMaxScore,
                QuizPassed = c.QuizPassed,
                QuizScorePercentage = c.QuizScore.HasValue && c.QuizMaxScore.HasValue && c.QuizMaxScore > 0
                    ? Math.Round((decimal)c.QuizScore.Value / c.QuizMaxScore.Value * 100, 2)
                    : null,
                SignedByName = c.SignedByName,
                SignedAt = c.SignedAt,
                CompletedOnTime = c.CompletedAt <= c.ScheduledTalk.DueDate,
                CertificateUrl = c.CertificateUrl,
                StartedLatitude = c.ScheduledTalk.StartedLatitude,
                StartedLongitude = c.ScheduledTalk.StartedLongitude,
                StartedAccuracyMeters = c.ScheduledTalk.StartedAccuracyMeters,
                StartedLocationTimestamp = c.ScheduledTalk.StartedLocationTimestamp,
                CompletedLatitude = c.CompletedLatitude,
                CompletedLongitude = c.CompletedLongitude,
                CompletedAccuracyMeters = c.CompletedAccuracyMeters,
                CompletedLocationTimestamp = c.CompletedLocationTimestamp
            }).ToList();

            return new PaginatedList<CompletionDetailDto>(items, totalCount, pageNumber, pageSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating completion report for tenant {TenantId}", tenantId);
            throw;
        }
    }
}
