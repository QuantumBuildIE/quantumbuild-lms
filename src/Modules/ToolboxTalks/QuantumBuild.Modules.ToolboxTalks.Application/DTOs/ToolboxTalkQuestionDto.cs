using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for a toolbox talk quiz question
/// </summary>
public record ToolboxTalkQuestionDto
{
    public Guid Id { get; init; }
    public Guid ToolboxTalkId { get; init; }
    public int QuestionNumber { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public QuestionType QuestionType { get; init; }
    public string QuestionTypeDisplay { get; init; } = string.Empty;

    /// <summary>
    /// Parsed options for multiple choice questions
    /// </summary>
    public List<string>? Options { get; init; }

    /// <summary>
    /// The correct answer (only visible to admins, not employees taking the quiz)
    /// </summary>
    public string? CorrectAnswer { get; init; }

    public int Points { get; init; }
    public ContentSource Source { get; init; }
    public string SourceDisplay { get; init; } = string.Empty;
    public bool IsFromVideoFinalPortion { get; init; }
    public string? VideoTimestamp { get; init; }
}
