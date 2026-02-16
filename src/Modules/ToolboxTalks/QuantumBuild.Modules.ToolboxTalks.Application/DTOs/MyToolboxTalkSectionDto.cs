namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// Section DTO for employee portal with progress tracking
/// </summary>
public record MyToolboxTalkSectionDto
{
    public Guid SectionId { get; init; }
    public int SectionNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool RequiresAcknowledgment { get; init; }

    // Progress
    public bool IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
    public int TimeSpentSeconds { get; init; }
}
