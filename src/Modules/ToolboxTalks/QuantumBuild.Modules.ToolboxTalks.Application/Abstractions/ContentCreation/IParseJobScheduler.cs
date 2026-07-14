namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;

/// <summary>
/// Schedules content parse background jobs (fire-and-forget).
/// Implemented in Infrastructure via Hangfire; consumed by handlers that
/// should not reference the concrete job classes.
/// </summary>
public interface IParseJobScheduler
{
    /// <summary>
    /// Enqueues a VideoTranscriptionJobForTalk then ContentCreationParseJobForTalk chain.
    /// Returns immediately — transcription and parse run asynchronously.
    /// </summary>
    string EnqueueVideoTranscriptionJob(Guid talkId, Guid tenantId);
}
