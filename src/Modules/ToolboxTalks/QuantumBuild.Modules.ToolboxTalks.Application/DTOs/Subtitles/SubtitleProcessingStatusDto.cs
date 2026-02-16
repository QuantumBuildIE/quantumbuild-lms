using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Subtitles;

/// <summary>
/// DTO for returning the status of a subtitle processing job
/// </summary>
public class SubtitleProcessingStatusDto
{
    /// <summary>
    /// The unique identifier of the processing job
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// The toolbox talk this job belongs to
    /// </summary>
    public Guid ToolboxTalkId { get; set; }

    /// <summary>
    /// Current status of the overall job
    /// </summary>
    public SubtitleProcessingStatus Status { get; set; }

    /// <summary>
    /// Overall completion percentage (0-100)
    /// </summary>
    public int OverallPercentage { get; set; }

    /// <summary>
    /// Description of the current processing step
    /// </summary>
    public string CurrentStep { get; set; } = string.Empty;

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When processing started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When processing completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total number of subtitle entries generated
    /// </summary>
    public int TotalSubtitles { get; set; }

    /// <summary>
    /// Status of each language translation
    /// </summary>
    public List<LanguageStatusDto> Languages { get; set; } = new();
}

/// <summary>
/// DTO for returning the status of a single language translation
/// </summary>
public class LanguageStatusDto
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
    /// Current status of this translation
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

    /// <summary>
    /// Error message if translation failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}
