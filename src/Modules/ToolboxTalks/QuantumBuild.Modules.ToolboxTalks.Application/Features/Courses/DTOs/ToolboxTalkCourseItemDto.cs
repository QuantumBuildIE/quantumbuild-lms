namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

/// <summary>
/// DTO for a course item with talk details for display
/// </summary>
public record ToolboxTalkCourseItemDto
{
    public Guid Id { get; init; }
    public Guid ToolboxTalkId { get; init; }
    public int OrderIndex { get; init; }
    public bool IsRequired { get; init; }

    // Talk details (for display)
    public string TalkCode { get; init; } = string.Empty;
    public string TalkTitle { get; init; } = string.Empty;
    public string? TalkDescription { get; init; }
    public bool TalkHasVideo { get; init; }
    public int TalkSectionCount { get; init; }
    public int TalkQuestionCount { get; init; }
}
