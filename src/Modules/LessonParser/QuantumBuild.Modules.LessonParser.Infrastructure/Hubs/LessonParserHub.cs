using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time lesson parser progress updates.
/// Clients connect before submitting a parse job and receive progress via their connectionId.
/// </summary>
[Authorize]
public class LessonParserHub : Hub
{
    private readonly ILogger<LessonParserHub> _logger;

    public LessonParserHub(ILogger<LessonParserHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to LessonParserHub",
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
            _logger.LogInformation("Client {ConnectionId} disconnected from LessonParserHub",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
