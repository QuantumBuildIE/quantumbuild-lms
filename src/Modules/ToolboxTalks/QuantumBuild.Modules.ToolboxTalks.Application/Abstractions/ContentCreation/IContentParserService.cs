using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;

/// <summary>
/// Parses raw text into logical sections using AI
/// </summary>
public interface IContentParserService
{
    /// <summary>
    /// Parse raw text into logical sections with titles, content, and suggested order.
    /// </summary>
    Task<ContentParseResult> ParseContentAsync(
        string rawText,
        InputMode inputModeHint,
        Guid tenantId,
        Guid? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Suggest whether the parsed sections should produce a Lesson or Course
    /// based on section count (threshold: 3+ sections = Course).
    /// </summary>
    OutputType SuggestOutputType(int sectionCount);
}

/// <summary>
/// Result of parsing raw content into sections
/// </summary>
public record ContentParseResult(
    bool Success,
    List<ParsedSection> Sections,
    OutputType SuggestedOutputType,
    string? ErrorMessage = null,
    int TokensUsed = 0);

/// <summary>
/// A single parsed section extracted from raw content
/// </summary>
public record ParsedSection(
    string Title,
    string Content,
    int SuggestedOrder);
