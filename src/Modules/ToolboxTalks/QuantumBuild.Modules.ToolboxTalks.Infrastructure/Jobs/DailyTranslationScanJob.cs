using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Daily Hangfire job that scans recently created/modified published talks for
/// translation gaps. Only checks talks touched in the last 25 hours (overlap
/// with 24-hour schedule to avoid gaps). Dispatches MissingTranslationsJob
/// per talk that has missing translations — does NOT translate directly.
/// </summary>
public class DailyTranslationScanJob
{
    private readonly ICoreDbContext _coreDbContext;
    private readonly IToolboxTalksDbContext _toolboxTalksDbContext;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<DailyTranslationScanJob> _logger;

    public DailyTranslationScanJob(
        ICoreDbContext coreDbContext,
        IToolboxTalksDbContext toolboxTalksDbContext,
        ITenantRepository tenantRepository,
        ILogger<DailyTranslationScanJob> logger)
    {
        _coreDbContext = coreDbContext;
        _toolboxTalksDbContext = toolboxTalksDbContext;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting DailyTranslationScanJob");

        var cutoff = DateTime.UtcNow.AddHours(-25);
        var tenants = await _tenantRepository.GetAllActiveAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            try
            {
                await ProcessTenantAsync(tenant.Id, cutoff, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "DailyTranslationScanJob failed for tenant {TenantId}. Continuing to next tenant.",
                    tenant.Id);
            }
        }

        _logger.LogInformation("Completed DailyTranslationScanJob");
    }

    private async Task ProcessTenantAsync(
        Guid tenantId,
        DateTime cutoff,
        CancellationToken cancellationToken)
    {
        // Get all required languages from employee preferences (excluding "en")
        var requiredLanguageCodes = await _coreDbContext.Employees
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted
                && e.PreferredLanguage != null && e.PreferredLanguage != "en")
            .Select(e => e.PreferredLanguage!)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (requiredLanguageCodes.Count == 0)
            return;

        // Find published talks created/modified/generated in the last 25 hours
        var recentTalks = await _toolboxTalksDbContext.ToolboxTalks
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId && !t.IsDeleted
                && t.Status == ToolboxTalkStatus.Published
                && (t.CreatedAt >= cutoff
                    || t.UpdatedAt >= cutoff
                    || (t.ContentGeneratedAt != null && t.ContentGeneratedAt >= cutoff)))
            .Select(t => new { t.Id, t.SourceLanguageCode })
            .ToListAsync(cancellationToken);

        if (recentTalks.Count == 0)
            return;

        var jobsQueued = 0;

        foreach (var talk in recentTalks)
        {
            var sourceLanguage = talk.SourceLanguageCode ?? "en";

            // Languages that need translations (excluding the source language)
            var languagesNeeded = requiredLanguageCodes
                .Where(l => !string.Equals(l, sourceLanguage, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (languagesNeeded.Count == 0)
                continue;

            // Check which translations already exist for this talk
            var existingLanguageCodes = await _toolboxTalksDbContext.ToolboxTalkTranslations
                .IgnoreQueryFilters()
                .Where(t => t.ToolboxTalkId == talk.Id && t.TenantId == tenantId && !t.IsDeleted)
                .Select(t => t.LanguageCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            var missingLanguages = languagesNeeded
                .Except(existingLanguageCodes, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingLanguages.Count > 0)
            {
                BackgroundJob.Enqueue<MissingTranslationsJob>(
                    job => job.ExecuteAsync(talk.Id, tenantId, null, CancellationToken.None));
                jobsQueued++;
            }
        }

        _logger.LogInformation(
            "DailyTranslationScan: tenant {TenantId} — checked {RecentCount} recent talks, queued {JobCount} translation jobs",
            tenantId, recentTalks.Count, jobsQueued);
    }
}
