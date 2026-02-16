using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a video translation request/status for a toolbox talk.
/// Tracks the translation of video content to a specific language using
/// external services like ElevenLabs for audio dubbing.
/// </summary>
public class ToolboxTalkVideoTranslation : TenantEntity
{
    /// <summary>
    /// Reference to the original toolbox talk
    /// </summary>
    public Guid ToolboxTalkId { get; set; }

    /// <summary>
    /// ISO 639-1 language code (e.g., "es", "fr", "pl", "ro")
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// URL to the original video that was translated
    /// </summary>
    public string OriginalVideoUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL to the translated/dubbed video (set when translation completes)
    /// </summary>
    public string? TranslatedVideoUrl { get; set; }

    /// <summary>
    /// Current status of the video translation
    /// </summary>
    public VideoTranslationStatus Status { get; set; } = VideoTranslationStatus.Pending;

    /// <summary>
    /// External project/job ID from translation service (e.g., ElevenLabs project ID)
    /// Used for polling status and retrieving results
    /// </summary>
    public string? ExternalProjectId { get; set; }

    /// <summary>
    /// Error message if translation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the translation was completed (null if not yet completed)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// The original toolbox talk that the video belongs to
    /// </summary>
    public ToolboxTalk ToolboxTalk { get; set; } = null!;
}
