using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a subtitle processing job that transcribes video audio
/// and generates translated SRT subtitle files for multiple languages.
/// </summary>
public class SubtitleProcessingJob : TenantEntity
{
    /// <summary>
    /// Reference to the toolbox talk this subtitle job belongs to
    /// </summary>
    public Guid ToolboxTalkId { get; set; }

    /// <summary>
    /// Current status of the overall processing job
    /// </summary>
    public SubtitleProcessingStatus Status { get; set; } = SubtitleProcessingStatus.Pending;

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// URL of the source video to be transcribed
    /// </summary>
    public string SourceVideoUrl { get; set; } = string.Empty;

    /// <summary>
    /// Type of video source (GoogleDrive, AzureBlob, DirectUrl)
    /// </summary>
    public SubtitleVideoSourceType VideoSourceType { get; set; }

    /// <summary>
    /// When the processing job started (null if not yet started)
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the processing job completed (null if not yet completed)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total number of subtitles generated from transcription
    /// </summary>
    public int TotalSubtitles { get; set; }

    /// <summary>
    /// The English SRT content generated from transcription.
    /// Stored in database for reference and re-translation if needed.
    /// </summary>
    public string? EnglishSrtContent { get; set; }

    /// <summary>
    /// URL to the English SRT file in storage
    /// </summary>
    public string? EnglishSrtUrl { get; set; }

    // Navigation properties

    /// <summary>
    /// The toolbox talk that this subtitle job belongs to
    /// </summary>
    public ToolboxTalk ToolboxTalk { get; set; } = null!;

    /// <summary>
    /// Translations of the subtitles to different languages
    /// </summary>
    public ICollection<SubtitleTranslation> Translations { get; set; } = new List<SubtitleTranslation>();
}
