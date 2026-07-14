namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Translation;

/// <summary>
/// One entry in the <c>ToolboxTalkTranslation.TranslatedSections</c> JSON array.
/// Shared across every read/write site of that blob so provenance fields round-trip
/// consistently instead of being silently dropped by a site using a narrower shape.
/// </summary>
public sealed record TranslatedSectionEntry
{
    public Guid SectionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
}
