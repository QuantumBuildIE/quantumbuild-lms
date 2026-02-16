using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for a toolbox talk section
/// </summary>
public record ToolboxTalkSectionDto
{
    public Guid Id { get; init; }
    public Guid ToolboxTalkId { get; init; }
    public int SectionNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool RequiresAcknowledgment { get; init; }
    public ContentSource Source { get; init; }
    public string SourceDisplay { get; init; } = string.Empty;
    public string? VideoTimestamp { get; init; }
}
