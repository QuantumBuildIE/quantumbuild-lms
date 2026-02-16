namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Status of a video translation request
/// </summary>
public enum VideoTranslationStatus
{
    /// <summary>
    /// Translation request is pending and not yet started
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Translation is currently being processed by external service
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Translation has been completed successfully
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Translation failed due to an error
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Automatic translation not possible, manual translation required
    /// </summary>
    ManualRequired = 5
}
