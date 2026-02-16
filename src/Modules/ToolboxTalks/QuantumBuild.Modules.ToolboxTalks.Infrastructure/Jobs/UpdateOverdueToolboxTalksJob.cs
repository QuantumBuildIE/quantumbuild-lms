using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Hangfire background job for marking overdue toolbox talks.
/// Runs hourly to update the status of talks that have passed their due date.
/// </summary>
public class UpdateOverdueToolboxTalksJob
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ILogger<UpdateOverdueToolboxTalksJob> _logger;

    public UpdateOverdueToolboxTalksJob(
        IToolboxTalksDbContext dbContext,
        ILogger<UpdateOverdueToolboxTalksJob> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Executes the overdue status update job.
    /// Marks Pending and InProgress talks as Overdue if past their due date.
    /// </summary>
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting UpdateOverdueToolboxTalksJob");

        var today = DateTime.UtcNow.Date;

        try
        {
            // NOTE: We use IgnoreQueryFilters() because this runs in a Hangfire background job
            // where ICurrentUserService.TenantId is not set. This job processes all tenants,
            // so we explicitly filter by IsDeleted only.
            var count = await _dbContext.ScheduledTalks
                .IgnoreQueryFilters()
                .Where(st => !st.IsDeleted)
                .Where(st => st.Status == ScheduledTalkStatus.Pending ||
                             st.Status == ScheduledTalkStatus.InProgress)
                .Where(st => st.DueDate.Date < today)
                .ExecuteUpdateAsync(
                    st => st.SetProperty(x => x.Status, ScheduledTalkStatus.Overdue),
                    cancellationToken);

            if (count > 0)
            {
                _logger.LogInformation(
                    "Marked {Count} scheduled talks as overdue",
                    count);
            }
            else
            {
                _logger.LogDebug("No scheduled talks to mark as overdue");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating overdue scheduled talks");
            throw;
        }

        _logger.LogInformation("Completed UpdateOverdueToolboxTalksJob");
    }
}
