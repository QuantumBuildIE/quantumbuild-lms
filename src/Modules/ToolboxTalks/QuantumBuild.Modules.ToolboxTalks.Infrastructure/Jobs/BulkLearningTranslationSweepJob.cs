using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Off-peak Hangfire job that sweeps Draft learnings created by BulkSopImportJob
/// (flagged via ToolboxTalk.BulkTranslationPendingSince) and enqueues MissingTranslationsJob
/// for each, into the tenant's employee languages. Mirrors DailyTranslationScanJob's
/// iterate-tenants / per-tenant-try-catch / enqueue-don't-translate-inline shape, but reads
/// its work list from the pending flag instead of scanning recently-touched Published talks —
/// bulk-created learnings stay Draft, so they would never be picked up by that scan (see
/// docs/translation-scan-behaviour-recon.md).
///
/// Throttled to <see cref="MaxLearningsPerTenantPerRun"/> pending learnings per tenant per run
/// so a large bulk batch spreads across multiple nightly runs rather than dumping
/// N learnings x M employee languages of translation calls into one window at once. The pending
/// flag is cleared only for the learnings actually enqueued this run, so the remainder is picked
/// up automatically on the next run (FIFO by BulkTranslationPendingSince).
///
/// A tenant with no employees preferring a non-English language is a clean no-op: the flag is
/// left set (not cleared) so those learnings are swept later if/when such an employee is added,
/// rather than being silently dropped.
/// </summary>
public class BulkLearningTranslationSweepJob
{
    /// <summary>
    /// Cap on how many pending learnings are enqueued per tenant per run. Each enqueue fans out
    /// internally (inside MissingTranslationsJob) to every one of the tenant's employee
    /// languages, so the real per-run ceiling is roughly Cap x employee-language-count
    /// translation calls per tenant — bounded and predictable rather than unbounded. 10 clears a
    /// typical bulk batch (a few dozen SOPs) within 2-3 nightly runs while keeping any single
    /// night's queue addition modest.
    /// </summary>
    private const int MaxLearningsPerTenantPerRun = 10;

    private readonly ICoreDbContext _coreDbContext;
    private readonly IToolboxTalksDbContext _toolboxTalksDbContext;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<BulkLearningTranslationSweepJob> _logger;

    public BulkLearningTranslationSweepJob(
        ICoreDbContext coreDbContext,
        IToolboxTalksDbContext toolboxTalksDbContext,
        ITenantRepository tenantRepository,
        ILogger<BulkLearningTranslationSweepJob> logger)
    {
        _coreDbContext = coreDbContext;
        _toolboxTalksDbContext = toolboxTalksDbContext;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting BulkLearningTranslationSweepJob");

        var tenants = await _tenantRepository.GetAllActiveAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            try
            {
                await ProcessTenantAsync(tenant.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "BulkLearningTranslationSweepJob failed for tenant {TenantId}. Continuing to next tenant.",
                    tenant.Id);
            }
        }

        _logger.LogInformation("Completed BulkLearningTranslationSweepJob");
    }

    private async Task ProcessTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // Same employee-language resolution DailyTranslationScanJob uses (excluding "en") —
        // a tenant-level pre-check, not a per-talk one, since we haven't loaded any talks yet.
        var requiredLanguageCodes = await _coreDbContext.Employees
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted
                && e.PreferredLanguage != null && e.PreferredLanguage != "en")
            .Select(e => e.PreferredLanguage!)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (requiredLanguageCodes.Count == 0)
        {
            // Clean no-op: nothing to translate into. Flags are left set so this tenant's
            // pending learnings are picked up automatically once a qualifying employee exists.
            return;
        }

        var pendingTalkIds = await _toolboxTalksDbContext.ToolboxTalks
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted && t.BulkTranslationPendingSince != null)
            .OrderBy(t => t.BulkTranslationPendingSince)
            .Take(MaxLearningsPerTenantPerRun)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        if (pendingTalkIds.Count == 0)
            return;

        foreach (var talkId in pendingTalkIds)
        {
            // One enqueue per learning — MissingTranslationsJob itself resolves and fans out to
            // every language the talk is missing, exactly as every other caller of this job does
            // (see docs/bulk-translation-job-recon.md §6).
            BackgroundJob.Enqueue<MissingTranslationsJob>(
                job => job.ExecuteAsync(talkId, tenantId, null, CancellationToken.None));
        }

        var clearedCount = await _toolboxTalksDbContext.ToolboxTalks
            .IgnoreQueryFilters()
            .Where(t => pendingTalkIds.Contains(t.Id))
            .ExecuteUpdateAsync(
                t => t.SetProperty(x => x.BulkTranslationPendingSince, (DateTimeOffset?)null),
                cancellationToken);

        _logger.LogInformation(
            "BulkLearningTranslationSweepJob: tenant {TenantId} — enqueued {Count} translation job(s), cleared {ClearedCount} pending flag(s)",
            tenantId, pendingTalkIds.Count, clearedCount);
    }
}
