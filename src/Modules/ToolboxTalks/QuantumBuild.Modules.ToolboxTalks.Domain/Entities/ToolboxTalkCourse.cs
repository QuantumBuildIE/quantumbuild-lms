using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Represents a course that groups multiple toolbox talks into an ordered learning path.
/// Courses can require sequential completion and support refresher scheduling.
/// </summary>
public class ToolboxTalkCourse : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequireSequentialCompletion { get; set; } = true;

    // Refresher settings (for Phase 4)
    public bool RequiresRefresher { get; set; } = false;
    public int RefresherIntervalMonths { get; set; } = 12;

    // Certificate settings (for Phase 5)
    public bool GenerateCertificate { get; set; } = false;

    // Auto-assignment settings
    public bool AutoAssignToNewEmployees { get; set; } = false;
    public int AutoAssignDueDays { get; set; } = 14;

    // Navigation properties
    public virtual ICollection<ToolboxTalkCourseItem> CourseItems { get; set; } = new List<ToolboxTalkCourseItem>();
    public virtual ICollection<ToolboxTalkCourseTranslation> Translations { get; set; } = new List<ToolboxTalkCourseTranslation>();
}
