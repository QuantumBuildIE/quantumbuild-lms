using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time content generation progress updates.
/// Clients can subscribe to specific toolbox talk generation jobs to receive progress updates.
/// </summary>
[Authorize]
public class ContentGenerationHub : Hub
{
    private readonly ILogger<ContentGenerationHub> _logger;

    public ContentGenerationHub(ILogger<ContentGenerationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribes the client to receive updates for a specific toolbox talk's content generation.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID to subscribe to</param>
    public async Task SubscribeToToolboxTalk(Guid toolboxTalkId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"content-generation-{toolboxTalkId}");
        _logger.LogInformation(
            "Client {ConnectionId} subscribed to content generation for toolbox talk {ToolboxTalkId}",
            Context.ConnectionId, toolboxTalkId);
    }

    /// <summary>
    /// Unsubscribes the client from a specific toolbox talk's content generation updates.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID to unsubscribe from</param>
    public async Task UnsubscribeFromToolboxTalk(Guid toolboxTalkId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"content-generation-{toolboxTalkId}");
        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from content generation for toolbox talk {ToolboxTalkId}",
            Context.ConnectionId, toolboxTalkId);
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to ContentGenerationHub",
            Context.ConnectionId);

        await base.OnConnectedAsync();
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
            _logger.LogInformation("Client {ConnectionId} disconnected from ContentGenerationHub",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
