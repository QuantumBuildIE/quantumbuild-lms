namespace QuantumBuild.Modules.LessonParser.Application.Abstractions;

/// <summary>
/// Deserialized Claude AI response representing a parsed course structure
/// </summary>
public record ParsedCourse
{
    public string CourseTitle { get; init; } = string.Empty;
    public List<ParsedTopic> Topics { get; init; } = new();
}

public record ParsedTopic
{
    public string Title { get; init; } = string.Empty;
    public List<ParsedSection> Sections { get; init; } = new();
    public List<ParsedQuestion> Questions { get; init; } = new();
}

public record ParsedSection
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public record ParsedQuestion
{
    public string Question { get; init; } = string.Empty;
    public List<string> Options { get; init; } = new();
    public int CorrectOptionIndex { get; init; }
}
