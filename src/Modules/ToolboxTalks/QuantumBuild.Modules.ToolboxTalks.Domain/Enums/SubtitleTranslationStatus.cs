namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Status of a subtitle translation for a specific language
/// </summary>
public enum SubtitleTranslationStatus
{
    /// <summary>
    /// Translation is pending and not yet started
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Translation is currently in progress
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Translation has been completed successfully
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Translation failed due to an error
    /// </summary>
    Failed = 4
}
