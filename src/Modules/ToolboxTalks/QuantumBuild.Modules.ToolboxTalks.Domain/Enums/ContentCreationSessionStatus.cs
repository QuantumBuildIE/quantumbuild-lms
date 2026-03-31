namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Status of a content creation session as it moves through the wizard pipeline
/// </summary>
public enum ContentCreationSessionStatus
{
    Draft = 1,
    Parsing = 2,
    Parsed = 3,
    TranslatingValidating = 4,
    Validated = 5,
    GeneratingQuiz = 6,
    QuizGenerated = 7,
    Publishing = 8,
    Completed = 9,
    Abandoned = 10,
    Failed = 11,
    Transcribing = 12
}
