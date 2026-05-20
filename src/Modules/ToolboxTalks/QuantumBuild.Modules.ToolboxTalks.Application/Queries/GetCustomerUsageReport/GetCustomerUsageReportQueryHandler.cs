using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetCustomerUsageReport;

public class GetCustomerUsageReportQueryHandler
    : IRequestHandler<GetCustomerUsageReportQuery, CustomerUsageReportDto>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly ICoreDbContext _coreContext;

    public GetCustomerUsageReportQueryHandler(
        IToolboxTalksDbContext context,
        ICoreDbContext coreContext)
    {
        _context = context;
        _coreContext = coreContext;
    }

    public async Task<CustomerUsageReportDto> Handle(
        GetCustomerUsageReportQuery request,
        CancellationToken cancellationToken)
    {
        // Load singleton state row to get LastReviewedAt
        var state = await _context.CustomerUsageReportStates
            .IgnoreQueryFilters()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastReviewedAt = state?.LastReviewedAt;

        // Resolve effective comparison date: caller override → stored LastReviewedAt → 30 days ago
        var comparisonDate = request.ComparisonDate
            ?? lastReviewedAt
            ?? DateTimeOffset.UtcNow.AddDays(-30);

        // DateTime equivalent for comparing against BaseEntity.CreatedAt (stored UTC, typed DateTime)
        var comparisonUtc = comparisonDate.UtcDateTime;

        // ── All non-deleted tenants ─────────────────────────────────────────
        var tenants = await _coreContext.Tenants
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted)
            .Select(t => new { t.Id, t.Name, t.CreatedAt })
            .ToListAsync(cancellationToken);

        var tenantIds = tenants.Select(t => t.Id).ToList();

        // ── Active employee counts grouped by TenantId ──────────────────────
        // IgnoreQueryFilters disables the soft-delete filter, so we re-apply !IsDeleted manually.
        var employeeCounts = await _coreContext.Employees
            .IgnoreQueryFilters()
            .Where(e => !e.IsDeleted && tenantIds.Contains(e.TenantId))
            .GroupBy(e => e.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        // ── Total learnings per tenant (all statuses, !IsDeleted) ───────────
        var totalLearnings = await _context.ToolboxTalks
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted && tenantIds.Contains(t.TenantId))
            .GroupBy(t => t.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        // ── New learnings per tenant (CreatedAt > comparisonDate) ───────────
        var newLearnings = await _context.ToolboxTalks
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted && tenantIds.Contains(t.TenantId) && t.CreatedAt > comparisonUtc)
            .GroupBy(t => t.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        // ── Completions per tenant (CompletedAt > comparisonDate) ───────────
        // Join through ScheduledTalk (TenantEntity) only to resolve TenantId —
        // completions are historical facts and must be counted even when the
        // parent ScheduledTalk was later soft-deleted (e.g. via course-assignment deletion).
        var completions = await _context.ScheduledTalkCompletions
            .IgnoreQueryFilters()
            .Join(
                _context.ScheduledTalks.IgnoreQueryFilters(),
                c => c.ScheduledTalkId,
                st => st.Id,
                (c, st) => new { TenantId = st.TenantId, c.CompletedAt })
            .Where(x => x.CompletedAt > comparisonUtc && tenantIds.Contains(x.TenantId))
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);

        // ── Last login per tenant (max User.LastLoginAt, nullable) ──────────
        // Users don't inherit BaseEntity so there is no global soft-delete filter on them.
        // Exclude SuperUser system accounts (TenantId == Guid.Empty).
        var lastLogins = await _coreContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId != Guid.Empty && !u.IsSuperUser)
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, LastLoginAt = g.Max(u => u.LastLoginAt) })
            .ToDictionaryAsync(x => x.TenantId, x => x.LastLoginAt, cancellationToken);

        // ── Stitch results in memory ─────────────────────────────────────────
        var cutoff = DateTimeOffset.UtcNow.AddDays(-30);

        var rows = tenants
            .OrderBy(t => t.Name)
            .Select(t =>
            {
                var lastLogin = lastLogins.GetValueOrDefault(t.Id);
                var tenantAge = DateTimeOffset.UtcNow - new DateTimeOffset(t.CreatedAt, TimeSpan.Zero);
                var isOldTenant = tenantAge.TotalDays > 30;
                var isAtRisk = isOldTenant && (lastLogin is null || lastLogin < cutoff);

                return new TenantUsageRowDto
                {
                    TenantId = t.Id,
                    TenantName = t.Name,
                    SignUpDate = t.CreatedAt,
                    ActiveEmployeeCount = employeeCounts.GetValueOrDefault(t.Id),
                    TotalLearnings = totalLearnings.GetValueOrDefault(t.Id),
                    NewLearnings = newLearnings.GetValueOrDefault(t.Id),
                    Completions = completions.GetValueOrDefault(t.Id),
                    LastLoginAt = lastLogin,
                    IsAtRisk = isAtRisk
                };
            })
            .ToList();

        return new CustomerUsageReportDto
        {
            LastReviewedAt = lastReviewedAt,
            ComparisonDate = comparisonDate,
            Rows = rows
        };
    }
}
