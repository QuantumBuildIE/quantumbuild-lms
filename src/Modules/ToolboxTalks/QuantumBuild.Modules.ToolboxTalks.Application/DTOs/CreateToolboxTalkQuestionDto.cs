using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for creating a new toolbox talk quiz question
/// </summary>
public record CreateToolboxTalkQuestionDto
{
    public int QuestionNumber { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public QuestionType QuestionType { get; init; } = QuestionType.MultipleChoice;

    /// <summary>
    /// Options for multiple choice questions (as a list)
    /// </summary>
    public List<string>? Options { get; init; }

    public string CorrectAnswer { get; init; } = string.Empty;
    public int Points { get; init; } = 1;
    public ContentSource Source { get; init; } = ContentSource.Manual;
    public bool IsFromVideoFinalPortion { get; init; } = false;
    public string? VideoTimestamp { get; init; }
}
