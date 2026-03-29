using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Monthly Hangfire job that aggregates raw AiUsageLog rows older than 3 months
/// into AiUsageSummary rows, then deletes the aggregated raw rows.
/// Runs on the 1st of each month at 3am UTC.
/// </summary>
public class AggregateAiUsageJob(
    IToolboxTalksDbContext dbContext,
    ILogger<AggregateAiUsageJob> logger)
{
    [AutomaticRetry(Attempts = 3)]
    [Queue("maintenance")]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Starting AggregateAiUsageJob");

            var cutoff = DateTimeOffset.UtcNow.AddMonths(-3);

            var rawLogs = await dbContext.AiUsageLogs
                .IgnoreQueryFilters()
                .Where(l => !l.IsDeleted && l.CalledAt < cutoff)
                .ToListAsync(cancellationToken);

            if (rawLogs.Count == 0)
            {
                logger.LogInformation("AggregateAiUsageJob: no rows older than 3 months to aggregate");
                return;
            }

            var groups = rawLogs
                .GroupBy(l => new
                {
                    l.TenantId,
                    Date = DateOnly.FromDateTime(l.CalledAt.UtcDateTime.Date),
                    l.OperationCategory,
                    l.ModelId
                })
                .ToList();

            // Load existing summaries for all relevant keys in one query
            var existingSummaries = await dbContext.AiUsageSummaries
                .IgnoreQueryFilters()
                .Where(s => !s.IsDeleted)
                .ToListAsync(cancellationToken);

            var summaryLookup = existingSummaries
                .ToDictionary(s => (s.TenantId, s.Date, s.OperationCategory, s.ModelId));

            var summariesWritten = 0;

            foreach (var group in groups)
            {
                var key = (group.Key.TenantId, group.Key.Date, group.Key.OperationCategory, group.Key.ModelId);

                if (summaryLookup.TryGetValue(key, out var existing))
                {
                    existing.TotalCalls += group.Count();
                    existing.TotalInputTokens += group.Sum(l => (long)l.InputTokens);
                    existing.TotalOutputTokens += group.Sum(l => (long)l.OutputTokens);
                    existing.SystemCallCount += group.Count(l => l.IsSystemCall);
                }
                else
                {
                    var summary = new AiUsageSummary
                    {
                        TenantId = group.Key.TenantId,
                        Date = group.Key.Date,
                        OperationCategory = group.Key.OperationCategory,
                        ModelId = group.Key.ModelId,
                        TotalCalls = group.Count(),
                        TotalInputTokens = group.Sum(l => (long)l.InputTokens),
                        TotalOutputTokens = group.Sum(l => (long)l.OutputTokens),
                        SystemCallCount = group.Count(l => l.IsSystemCall)
                    };

                    dbContext.AiUsageSummaries.Add(summary);
                    summaryLookup[key] = summary;
                }

                summariesWritten++;
            }

            // Delete aggregated raw rows
            dbContext.AiUsageLogs.RemoveRange(rawLogs);

            // Single SaveChanges — upserts and deletes in the same transaction
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "AggregateAiUsageJob completed: {RawRows} raw rows aggregated into {SummaryRows} summary rows",
                rawLogs.Count, summariesWritten);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AggregateAiUsageJob failed");
        }
    }
}
