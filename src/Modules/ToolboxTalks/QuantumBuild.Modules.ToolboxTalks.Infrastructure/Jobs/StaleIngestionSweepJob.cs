using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Daily Hangfire recurring job that force-terminates RegulatoryDocument rows abandoned mid
/// ingestion — the case RequirementIngestionJob's own catch block cannot cover: the worker
/// process that set LastIngestionStatus = Ingesting was killed (OOM, container recycle, deploy
/// restart) before any of that job's code, including its catch block, ran again. Nothing else
/// in the codebase sweeps for this. Mirrors ExpiredSessionCleanupJob's shape (age-based query,
/// forced terminal write, single batched save) — see docs/ingestion-terminal-state-recon.md §7,
/// which identified that job as the closest existing precedent for this kind of sweep.
///
/// Staleness threshold: 60 minutes. RequirementIngestionJob's own worst-case legitimate
/// runtime is roughly 40 minutes (up to 8 Claude/fetch calls at a 5-minute HttpClient timeout
/// each, back-to-back — see recon (c)/§5). 60 minutes leaves a ~20-minute margin above that
/// worst case so this sweep can never kill a still-healthy long-running job — the same mistake
/// the frontend polling fix (Chunk B) had to correct with its own 30-minute-was-too-short
/// threshold.
/// </summary>
public class StaleIngestionSweepJob(
    IToolboxTalksDbContext dbContext,
    ILogger<StaleIngestionSweepJob> logger)
{
    private static readonly TimeSpan StalenessThreshold = TimeSpan.FromMinutes(60);

    [AutomaticRetry(Attempts = 1)]
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("StaleIngestionSweepJob starting");

        var cutoff = DateTime.UtcNow - StalenessThreshold;

        var abandoned = await dbContext.RegulatoryDocuments
            .Where(d => d.LastIngestionStatus == RegulatoryIngestionStatus.Ingesting)
            .Where(d => (d.UpdatedAt ?? d.CreatedAt) < cutoff)
            .ToListAsync(cancellationToken);

        if (abandoned.Count == 0)
        {
            logger.LogInformation("StaleIngestionSweepJob: no abandoned ingestion runs found");
            return;
        }

        foreach (var document in abandoned)
        {
            var stuckSince = document.UpdatedAt ?? document.CreatedAt;

            document.LastIngestionStatus = RegulatoryIngestionStatus.Failed;
            document.LastIngestionErrorCode = "ingestion_abandoned";
            document.LastIngestionErrorMessage =
                $"Ingestion did not reach a terminal state within {StalenessThreshold.TotalMinutes:0} minutes " +
                "and was force-terminated by the staleness sweep. The worker process likely died mid-run " +
                "(deploy restart, OOM kill, container recycle). Retry ingestion.";

            logger.LogWarning(
                "Marking abandoned ingestion Failed for document {DocumentId} (stuck on Ingesting since {StuckSince})",
                document.Id, stuckSince);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "StaleIngestionSweepJob completed: marked {Count} abandoned document(s) Failed",
            abandoned.Count);
    }
}
