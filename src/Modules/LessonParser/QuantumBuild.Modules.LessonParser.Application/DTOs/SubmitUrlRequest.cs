namespace QuantumBuild.Modules.LessonParser.Application.DTOs;

/// <summary>
/// Request to parse content from a URL
/// </summary>
public record SubmitUrlRequest
{
    public string Url { get; init; } = string.Empty;
}
