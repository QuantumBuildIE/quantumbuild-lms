using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;

/// <summary>
/// Orchestrates the subtitle processing workflow including:
/// - Downloading/accessing video
/// - Transcribing audio (ElevenLabs)
/// - Generating SRT files
/// - Translating to target languages (Claude)
/// - Uploading to storage (GitHub)
/// - Reporting progress via SignalR
/// </summary>
public interface ISubtitleProcessingOrchestrator
{
    /// <summary>
    /// Starts a new subtitle processing job for a toolbox talk.
    /// Creates the job record and queues it for background processing.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk to process subtitles for</param>
    /// <param name="videoUrl">URL to the video file</param>
    /// <param name="sourceType">Type of video source</param>
    /// <param name="targetLanguages">Languages to translate to (English is always included)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the created processing job</returns>
    Task<Guid> StartProcessingAsync(
        Guid toolboxTalkId,
        string videoUrl,
        SubtitleVideoSourceType sourceType,
        List<string> targetLanguages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes a subtitle job. Called by Hangfire background job.
    /// </summary>
    /// <param name="jobId">The job ID to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes retried translations. Called by Hangfire background job.
    /// </summary>
    /// <param name="jobId">The job ID to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ProcessRetryAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a subtitle processing job for a toolbox talk.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Status DTO or null if no job exists</returns>
    Task<SubtitleProcessingStatusDto?> GetStatusAsync(
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an active subtitle processing job.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if cancelled, false if no active job found</returns>
    /// <exception cref="InvalidOperationException">Thrown if job already completed or failed</exception>
    Task<bool> CancelProcessingAsync(
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retries failed translations for an existing job.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The job ID if retry started, null if no failed translations</returns>
    /// <exception cref="InvalidOperationException">Thrown if no job exists or no failed translations</exception>
    Task<Guid?> RetryFailedTranslationsAsync(
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the SRT content for a specific language translation.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="languageCode">ISO 639-1 language code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SRT content if found and completed, null otherwise</returns>
    Task<string?> GetSrtContentAsync(
        Guid toolboxTalkId,
        string languageCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Translates existing subtitles to additional languages that are missing.
    /// Used when new employee languages are added after subtitle processing was completed.
    /// Finds the latest completed subtitle job, takes the English SRT, and translates
    /// it to each missing language, uploading the results to storage.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk ID</param>
    /// <param name="tenantId">The tenant ID (for query filter bypass in background jobs)</param>
    /// <param name="missingLanguageCodes">ISO 639-1 codes of languages to translate to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of languages successfully translated</returns>
    Task<int> TranslateMissingLanguagesAsync(
        Guid toolboxTalkId,
        Guid tenantId,
        List<string> missingLanguageCodes,
        CancellationToken cancellationToken = default);
}
