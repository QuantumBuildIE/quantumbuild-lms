namespace QuantumBuild.Modules.LessonParser.Application.Abstractions;

/// <summary>
/// Result of extracting text content from a document source
/// </summary>
public record ExtractionResult
{
    public string Content { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int CharacterCount { get; init; }
    public bool IsEmpty => string.IsNullOrWhiteSpace(Content);
}
