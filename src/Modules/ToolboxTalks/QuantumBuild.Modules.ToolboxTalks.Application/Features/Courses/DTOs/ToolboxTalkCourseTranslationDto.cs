namespace QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;

/// <summary>
/// DTO for a course translation
/// </summary>
public record ToolboxTalkCourseTranslationDto
{
    public Guid Id { get; init; }
    public string LanguageCode { get; init; } = string.Empty;
    public string TranslatedTitle { get; init; } = string.Empty;
    public string? TranslatedDescription { get; init; }
}
