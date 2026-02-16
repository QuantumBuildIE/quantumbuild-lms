using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a translated subtitle file for a specific language.
/// Each SubtitleProcessingJob can have multiple translations.
/// </summary>
public class SubtitleTranslation : BaseEntity
{
    /// <summary>
    /// Reference to the parent subtitle processing job
    /// </summary>
    public Guid SubtitleProcessingJobId { get; set; }

    /// <summary>
    /// Display name of the target language (e.g., "Spanish", "Polish")
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// ISO 639-1 language code (e.g., "es", "pl", "ro")
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Current status of this translation
    /// </summary>
    public SubtitleTranslationStatus Status { get; set; } = SubtitleTranslationStatus.Pending;

    /// <summary>
    /// Number of subtitles that have been translated
    /// </summary>
    public int SubtitlesProcessed { get; set; }

    /// <summary>
    /// Total number of subtitles to translate
    /// </summary>
    public int TotalSubtitles { get; set; }

    /// <summary>
    /// The translated SRT content.
    /// Stored in database for reference and quick retrieval.
    /// </summary>
    public string? SrtContent { get; set; }

    /// <summary>
    /// URL to the translated SRT file in storage (GitHub, Azure Blob, etc.)
    /// </summary>
    public string? SrtUrl { get; set; }

    /// <summary>
    /// Error message if translation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent subtitle processing job
    /// </summary>
    public SubtitleProcessingJob ProcessingJob { get; set; } = null!;
}
