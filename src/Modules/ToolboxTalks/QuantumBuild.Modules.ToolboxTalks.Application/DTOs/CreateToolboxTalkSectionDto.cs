using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for creating a new toolbox talk section
/// </summary>
public record CreateToolboxTalkSectionDto
{
    public int SectionNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool RequiresAcknowledgment { get; init; } = true;
    public ContentSource Source { get; init; } = ContentSource.Manual;
    public string? VideoTimestamp { get; init; }
}
