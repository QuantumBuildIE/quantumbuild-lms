namespace QuantumBuild.Modules.LessonParser.Application.Abstractions;

/// <summary>
/// Reports progress during lesson generation from extracted content
/// </summary>
public record LessonParseProgress
{
    public string Stage { get; init; } = string.Empty;
    public int PercentComplete { get; init; }
    public int CurrentTalk { get; init; }
    public int TotalTalks { get; init; }
}
