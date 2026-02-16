using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Join entity linking a ToolboxTalk to a ToolboxTalkCourse with ordering.
/// Each talk can appear in multiple courses but only once per course.
/// </summary>
public class ToolboxTalkCourseItem : BaseEntity
{
    public Guid CourseId { get; set; }
    public Guid ToolboxTalkId { get; set; }
    public int OrderIndex { get; set; }
    public bool IsRequired { get; set; } = true;

    // Navigation properties
    public virtual ToolboxTalkCourse Course { get; set; } = null!;
    public virtual ToolboxTalk ToolboxTalk { get; set; } = null!;
}
