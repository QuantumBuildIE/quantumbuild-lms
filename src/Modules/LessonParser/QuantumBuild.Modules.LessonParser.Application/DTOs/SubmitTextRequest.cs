namespace QuantumBuild.Modules.LessonParser.Application.DTOs;

/// <summary>
/// Request to parse content from raw text
/// </summary>
public record SubmitTextRequest
{
    public string Content { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}
