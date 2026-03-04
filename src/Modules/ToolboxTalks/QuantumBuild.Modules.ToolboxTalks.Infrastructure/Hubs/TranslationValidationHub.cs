using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;

/// <summary>
/// SignalR hub for real-time translation validation progress updates.
/// Clients can subscribe to specific validation runs to receive progress updates.
/// Route: /api/hubs/translation-validation
/// Groups: validation-{runId}
/// </summary>
[Authorize]
public class TranslationValidationHub : Hub
{
    private readonly ILogger<TranslationValidationHub> _logger;

    public TranslationValidationHub(ILogger<TranslationValidationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribes the client to receive updates for a specific validation run.
    /// </summary>
    /// <param name="validationRunId">The validation run ID to subscribe to</param>
    public async Task SubscribeToValidationRun(Guid validationRunId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"validation-{validationRunId}");
        _logger.LogInformation(
            "Client {ConnectionId} subscribed to validation run {ValidationRunId}",
            Context.ConnectionId, validationRunId);
    }

    /// <summary>
    /// Unsubscribes the client from a specific validation run's updates.
    /// </summary>
    /// <param name="validationRunId">The validation run ID to unsubscribe from</param>
    public async Task UnsubscribeFromValidationRun(Guid validationRunId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"validation-{validationRunId}");
        _logger.LogInformation(
            "Client {ConnectionId} unsubscribed from validation run {ValidationRunId}",
            Context.ConnectionId, validationRunId);
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to TranslationValidationHub",
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
            _logger.LogInformation("Client {ConnectionId} disconnected from TranslationValidationHub",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
