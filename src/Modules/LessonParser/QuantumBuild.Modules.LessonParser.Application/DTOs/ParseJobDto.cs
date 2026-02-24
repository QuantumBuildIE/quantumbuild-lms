namespace QuantumBuild.Modules.LessonParser.Application.DTOs;

/// <summary>
/// DTO representing a parse job for API responses
/// </summary>
public record ParseJobDto
{
    public Guid Id { get; init; }
    public string InputType { get; init; } = string.Empty;
    public string InputReference { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Guid? GeneratedCourseId { get; init; }
    public string? GeneratedCourseTitle { get; init; }
    public int TalksGenerated { get; init; }
    public string? ErrorMessage { get; init; }
    public string TranslationStatus { get; init; } = string.Empty;
    public string? TranslationLanguages { get; init; }
    public int TranslationsQueued { get; init; }
    public string? TranslationFailures { get; init; }
    public DateTime CreatedAt { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
}
