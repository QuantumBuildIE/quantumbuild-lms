namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;

/// <summary>
/// Schedules translation background jobs (fire-and-forget).
/// Implemented in Infrastructure via Hangfire; consumed by cross-module
/// callers that should not reference the concrete job classes.
/// </summary>
public interface ITranslationJobScheduler
{
    /// <summary>
    /// Enqueues a background job that detects missing translations for a
    /// toolbox talk and generates them (content + subtitles).
    /// Returns immediately — the actual translation runs asynchronously.
    /// </summary>
    /// <param name="toolboxTalkId">The talk to translate</param>
    /// <param name="tenantId">Tenant scope</param>
    /// <returns>The Hangfire job ID</returns>
    string EnqueueMissingTranslationsJob(Guid toolboxTalkId, Guid tenantId);
}
