namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Decision made by a human reviewer on a translation validation result
/// </summary>
public enum ReviewerDecision
{
    /// <summary>
    /// No decision made yet
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Reviewer accepted the translation as-is
    /// </summary>
    Accepted = 2,

    /// <summary>
    /// Reviewer rejected the translation
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// Reviewer edited the translation
    /// </summary>
    Edited = 4
}
