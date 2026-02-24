namespace QuantumBuild.Modules.LessonParser.Application.DTOs;

/// <summary>
/// Response returned when a parse job is successfully enqueued
/// </summary>
public record StartParseResponse
{
    public Guid JobId { get; init; }
    public string Message { get; init; } = string.Empty;
}
