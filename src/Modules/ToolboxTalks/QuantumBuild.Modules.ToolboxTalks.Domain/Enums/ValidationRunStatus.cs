namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Status of a translation validation run
/// </summary>
public enum ValidationRunStatus
{
    /// <summary>
    /// Run is queued and waiting to start
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Validation is currently in progress
    /// </summary>
    Running = 2,

    /// <summary>
    /// All sections validated successfully
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Validation failed due to an error
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Validation was cancelled by user
    /// </summary>
    Cancelled = 5
}
