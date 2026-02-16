using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a single slide (page) extracted from a PDF document for a toolbox talk slideshow.
/// Each slide contains the page image storage path and optionally the extracted text content.
/// </summary>
public class ToolboxTalkSlide : TenantEntity
{
    /// <summary>
    /// Reference to the parent toolbox talk
    /// </summary>
    public Guid ToolboxTalkId { get; set; }

    /// <summary>
    /// Page number from the PDF (1-based)
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Storage path for the slide image (e.g., R2 path or local path)
    /// </summary>
    public string ImageStoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Text content extracted from this PDF page via OCR or text extraction.
    /// Used as source for translations.
    /// </summary>
    public string? OriginalText { get; set; }

    // Navigation properties

    /// <summary>
    /// The parent toolbox talk
    /// </summary>
    public virtual ToolboxTalk ToolboxTalk { get; set; } = null!;

    /// <summary>
    /// Translations of the extracted text for this slide
    /// </summary>
    public virtual ICollection<ToolboxTalkSlideTranslation> Translations { get; set; } = new List<ToolboxTalkSlideTranslation>();
}
