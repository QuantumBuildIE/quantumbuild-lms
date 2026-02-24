namespace QuantumBuild.Modules.LessonParser.Domain.Enums;

/// <summary>
/// Status of translation queuing after a parse job completes
/// </summary>
public enum TranslationQueueStatus
{
    /// <summary>
    /// No employee preferred languages found — no translations needed
    /// </summary>
    NotRequired = 0,

    /// <summary>
    /// Translations enqueued successfully
    /// </summary>
    Queued = 1,

    /// <summary>
    /// Some translations failed while others succeeded
    /// </summary>
    PartialFailure = 2,

    /// <summary>
    /// All translations failed
    /// </summary>
    Failed = 3,

    /// <summary>
    /// All expected translations completed successfully
    /// </summary>
    Completed = 4
}
