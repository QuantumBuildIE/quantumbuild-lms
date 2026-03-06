using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Daily Hangfire recurring job that finds expired content creation sessions,
/// deletes their R2 files, and marks them as Abandoned.
/// </summary>
[AutomaticRetry(Attempts = 1)]
public class ExpiredSessionCleanupJob(
    IToolboxTalksDbContext dbContext,
    IR2StorageService r2StorageService,
    ILogger<ExpiredSessionCleanupJob> logger)
{
    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("ExpiredSessionCleanupJob starting");

        var terminalStatuses = new[]
        {
            ContentCreationSessionStatus.Completed,
            ContentCreationSessionStatus.Abandoned
        };

        var expiredSessions = await dbContext.ContentCreationSessions
            .Where(s => s.ExpiresAt < DateTime.UtcNow)
            .Where(s => !terminalStatuses.Contains(s.Status))
            .ToListAsync(cancellationToken);

        if (expiredSessions.Count == 0)
        {
            logger.LogInformation("ExpiredSessionCleanupJob: no expired sessions found");
            return;
        }

        var cleanedCount = 0;
        foreach (var session in expiredSessions)
        {
            try
            {
                await r2StorageService.DeleteSessionFilesAsync(
                    session.TenantId, session.Id, cancellationToken);

                session.Status = ContentCreationSessionStatus.Abandoned;
                cleanedCount++;

                logger.LogDebug(
                    "Cleaned up expired session {SessionId} for tenant {TenantId}",
                    session.Id, session.TenantId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to clean up session {SessionId}, will retry next run",
                    session.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ExpiredSessionCleanupJob completed: cleaned {CleanedCount} of {TotalCount} expired sessions",
            cleanedCount, expiredSessions.Count);
    }
}
