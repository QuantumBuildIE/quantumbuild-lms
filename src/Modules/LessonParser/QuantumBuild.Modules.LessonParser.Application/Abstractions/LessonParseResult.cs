namespace QuantumBuild.Modules.LessonParser.Application.Abstractions;

/// <summary>
/// Result of generating a course with talks from extracted content
/// </summary>
public record LessonParseResult
{
    public Guid CourseId { get; init; }
    public string CourseTitle { get; init; } = string.Empty;
    public int TalksGenerated { get; init; }
}
