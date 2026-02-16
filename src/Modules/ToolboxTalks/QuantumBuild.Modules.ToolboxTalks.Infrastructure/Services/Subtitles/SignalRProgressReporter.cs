using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;

/// <summary>
/// SignalR-based implementation of subtitle progress reporting.
/// Sends real-time progress updates to connected clients.
/// </summary>
public class SignalRProgressReporter : ISubtitleProgressReporter
{
    private readonly IHubContext<SubtitleProcessingHub> _hubContext;
    private readonly ILogger<SignalRProgressReporter> _logger;

    public SignalRProgressReporter(
        IHubContext<SubtitleProcessingHub> hubContext,
        ILogger<SignalRProgressReporter> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Reports a progress update to all clients subscribed to the job.
    /// </summary>
    public async Task ReportProgressAsync(
        Guid jobId,
        SubtitleProgressUpdate update,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .Group($"job-{jobId}")
                .SendAsync("ProgressUpdate", update, cancellationToken);

            _logger.LogDebug("Progress update sent for job {JobId}: {Status} - {Percentage}%",
                jobId, update.OverallStatus, update.OverallPercentage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send progress update for job {JobId}", jobId);
        }
    }
}
