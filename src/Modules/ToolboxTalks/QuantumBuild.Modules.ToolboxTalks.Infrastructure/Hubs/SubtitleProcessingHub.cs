using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time subtitle processing progress updates.
/// Clients can subscribe to specific processing jobs to receive progress updates.
/// </summary>
[Authorize]
public class SubtitleProcessingHub : Hub
{
    private readonly ILogger<SubtitleProcessingHub> _logger;

    public SubtitleProcessingHub(ILogger<SubtitleProcessingHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribes the client to receive updates for a specific processing job.
    /// </summary>
    /// <param name="jobId">The subtitle processing job ID to subscribe to</param>
    public async Task SubscribeToJob(Guid jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
        _logger.LogInformation("Client {ConnectionId} subscribed to job {JobId}",
            Context.ConnectionId, jobId);
    }

    /// <summary>
    /// Unsubscribes the client from a specific processing job.
    /// </summary>
    /// <param name="jobId">The subtitle processing job ID to unsubscribe from</param>
    public async Task UnsubscribeFromJob(Guid jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job-{jobId}");
        _logger.LogInformation("Client {ConnectionId} unsubscribed from job {JobId}",
            Context.ConnectionId, jobId);
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to SubtitleProcessingHub",
            Context.ConnectionId);

        await base.OnConnectedAsync();
    }
}
