using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time corpus run progress.
/// Clients subscribe to a specific run via SubscribeToCorpusRun.
/// Group pattern: "corpus-{corpusRunId}"
/// </summary>
[Authorize]
public class CorpusRunHub(ILogger<CorpusRunHub> logger) : Hub
{
    /// <summary>Subscribe to progress events for a corpus run.</summary>
    public async Task SubscribeToCorpusRun(Guid corpusRunId)
    {
        var groupName = $"corpus-{corpusRunId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        logger.LogDebug("Client {ConnectionId} subscribed to corpus run {RunId}", Context.ConnectionId, corpusRunId);
    }

    /// <summary>Unsubscribe from a corpus run group.</summary>
    public async Task UnsubscribeFromCorpusRun(Guid corpusRunId)
    {
        var groupName = $"corpus-{corpusRunId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    public override Task OnConnectedAsync()
    {
        logger.LogDebug("CorpusRunHub connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogDebug("CorpusRunHub disconnected: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
