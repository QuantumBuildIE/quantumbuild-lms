using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Stores translated title and description for a ToolboxTalkCourse.
/// One translation per language per course.
/// </summary>
public class ToolboxTalkCourseTranslation : BaseEntity
{
    public Guid CourseId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string TranslatedTitle { get; set; } = string.Empty;
    public string? TranslatedDescription { get; set; }

    // Navigation property
    public virtual ToolboxTalkCourse Course { get; set; } = null!;
}
