namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

/// <summary>
/// Full DTO for a toolbox talk course with all details including items and translations
/// </summary>
public record ToolboxTalkCourseDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public bool RequireSequentialCompletion { get; init; }
    public bool RequiresRefresher { get; init; }
    public int RefresherIntervalMonths { get; init; }
    public bool GenerateCertificate { get; init; }
    public bool AutoAssignToNewEmployees { get; init; }
    public int AutoAssignDueDays { get; init; }

    public int TalkCount { get; init; }
    public List<ToolboxTalkCourseItemDto> Items { get; init; } = new();
    public List<ToolboxTalkCourseTranslationDto> Translations { get; init; } = new();

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
