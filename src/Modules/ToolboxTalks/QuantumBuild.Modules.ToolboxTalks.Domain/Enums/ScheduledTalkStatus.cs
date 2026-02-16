namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Status of an individual scheduled talk assignment
/// </summary>
public enum ScheduledTalkStatus
{
    /// <summary>
    /// Talk has been assigned but not started
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Employee has started but not completed the talk
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// Employee has completed the talk successfully
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Talk has passed its due date without completion
    /// </summary>
    Overdue = 4,

    /// <summary>
    /// Talk has been cancelled (schedule was cancelled)
    /// </summary>
    Cancelled = 5
}
