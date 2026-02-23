namespace QuantumBuild.Modules.LessonParser.Domain.Enums;

/// <summary>
/// Status of a parse job in the processing workflow
/// </summary>
public enum ParseJobStatus
{
    /// <summary>
    /// Job is currently being processed by the AI
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Job completed successfully — course and talks generated
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Job failed with an error
    /// </summary>
    Failed = 3
}
