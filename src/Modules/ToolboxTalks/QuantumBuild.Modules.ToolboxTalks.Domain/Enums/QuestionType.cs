namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Type of quiz question for toolbox talk assessments
/// </summary>
public enum QuestionType
{
    /// <summary>
    /// Multiple choice question with predefined options
    /// </summary>
    MultipleChoice = 1,

    /// <summary>
    /// True or false question
    /// </summary>
    TrueFalse = 2,

    /// <summary>
    /// Short answer text response
    /// </summary>
    ShortAnswer = 3
}
