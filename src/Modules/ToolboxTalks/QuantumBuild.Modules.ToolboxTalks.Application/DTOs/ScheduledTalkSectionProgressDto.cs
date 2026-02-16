namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for section progress within a scheduled talk
/// </summary>
public record ScheduledTalkSectionProgressDto
{
    public Guid Id { get; init; }
    public Guid ScheduledTalkId { get; init; }
    public Guid SectionId { get; init; }
    public string SectionTitle { get; init; } = string.Empty;
    public int SectionNumber { get; init; }
    public bool IsRead { get; init; }
    public DateTime? ReadAt { get; init; }
    public int TimeSpentSeconds { get; init; }
}
