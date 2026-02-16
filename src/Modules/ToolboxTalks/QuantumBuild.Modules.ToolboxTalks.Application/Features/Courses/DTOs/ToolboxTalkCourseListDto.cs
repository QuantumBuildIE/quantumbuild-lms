namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

/// <summary>
/// Lightweight DTO for course list views
/// </summary>
public record ToolboxTalkCourseListDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public bool RequireSequentialCompletion { get; init; }
    public bool AutoAssignToNewEmployees { get; init; }
    public int TalkCount { get; init; }
    public int TranslationCount { get; init; }
    public DateTime CreatedAt { get; init; }
}
