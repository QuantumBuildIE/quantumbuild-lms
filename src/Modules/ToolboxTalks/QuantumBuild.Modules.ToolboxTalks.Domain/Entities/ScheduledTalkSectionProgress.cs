using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Tracks employee progress through a toolbox talk section.
/// Records which sections have been read and time spent on each.
/// </summary>
public class ScheduledTalkSectionProgress : BaseEntity
{
    /// <summary>
    /// The scheduled talk this progress belongs to
    /// </summary>
    public Guid ScheduledTalkId { get; set; }

    /// <summary>
    /// The section being tracked
    /// </summary>
    public Guid SectionId { get; set; }

    /// <summary>
    /// Whether the section has been read/completed
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// When the section was marked as read
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Total time spent on this section in seconds
    /// </summary>
    public int TimeSpentSeconds { get; set; } = 0;

    // Navigation properties

    /// <summary>
    /// The parent scheduled talk
    /// </summary>
    public ScheduledTalk ScheduledTalk { get; set; } = null!;

    /// <summary>
    /// The toolbox talk section
    /// </summary>
    public ToolboxTalkSection Section { get; set; } = null!;
}
