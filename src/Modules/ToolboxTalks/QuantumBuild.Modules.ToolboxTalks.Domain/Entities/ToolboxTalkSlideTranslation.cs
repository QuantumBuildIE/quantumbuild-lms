using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a translated version of the extracted text from a toolbox talk slide.
/// Each translation is for a specific language code.
/// </summary>
public class ToolboxTalkSlideTranslation : BaseEntity
{
    /// <summary>
    /// Reference to the parent slide
    /// </summary>
    public Guid SlideId { get; set; }

    /// <summary>
    /// ISO 639-1 language code (e.g., "es", "fr", "pl", "ro")
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// The translated text content for this slide in the target language
    /// </summary>
    public string TranslatedText { get; set; } = string.Empty;

    // Navigation properties

    /// <summary>
    /// The parent slide that was translated
    /// </summary>
    public virtual ToolboxTalkSlide Slide { get; set; } = null!;
}
