using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a translated version of the AI-generated HTML slideshow for a toolbox talk.
/// Each translation contains the complete HTML slideshow with translated content.
/// </summary>
public class ToolboxTalkSlideshowTranslation : BaseEntity
{
    /// <summary>
    /// Reference to the parent toolbox talk
    /// </summary>
    public Guid ToolboxTalkId { get; set; }

    /// <summary>
    /// ISO 639-1 language code (e.g., "es", "fr", "de")
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// The complete HTML slideshow with translated content
    /// </summary>
    public string TranslatedHtml { get; set; } = string.Empty;

    /// <summary>
    /// When the translation was generated
    /// </summary>
    public DateTime TranslatedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent toolbox talk
    /// </summary>
    public ToolboxTalk ToolboxTalk { get; set; } = null!;
}
