using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;

/// <summary>
/// Service for reporting subtitle processing progress to clients (e.g., via SignalR).
/// </summary>
public interface ISubtitleProgressReporter
{
    /// <summary>
    /// Reports progress update for a subtitle processing job.
    /// </summary>
    /// <param name="jobId">The subtitle processing job ID</param>
    /// <param name="update">Progress update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReportProgressAsync(
        Guid jobId,
        SubtitleProgressUpdate update,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Progress update data for a subtitle processing job
/// </summary>
public class SubtitleProgressUpdate
{
    /// <summary>
    /// Overall status of the processing job
    /// </summary>
    public SubtitleProcessingStatus OverallStatus { get; set; }

    /// <summary>
    /// Overall completion percentage (0-100)
    /// </summary>
    public int OverallPercentage { get; set; }

    /// <summary>
    /// Description of the current processing step
    /// </summary>
    public string CurrentStep { get; set; } = string.Empty;

    /// <summary>
    /// Error message if processing has failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Progress information for each language being translated
    /// </summary>
    public List<LanguageProgressInfo> Languages { get; set; } = new();
}

/// <summary>
/// Progress information for a single language translation
/// </summary>
public class LanguageProgressInfo
{
    /// <summary>
    /// Display name of the language (e.g., "Spanish")
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// ISO 639-1 language code (e.g., "es")
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Status of this language's translation
    /// </summary>
    public SubtitleTranslationStatus Status { get; set; }

    /// <summary>
    /// Completion percentage for this language (0-100)
    /// </summary>
    public int Percentage { get; set; }

    /// <summary>
    /// URL to the SRT file (when completed)
    /// </summary>
    public string? SrtUrl { get; set; }
}
