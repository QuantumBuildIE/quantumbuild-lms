namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Outcome of a translation validation check
/// </summary>
public enum ValidationOutcome
{
    /// <summary>
    /// Translation passed validation
    /// </summary>
    Pass = 1,

    /// <summary>
    /// Translation needs human review
    /// </summary>
    Review = 2,

    /// <summary>
    /// Translation failed validation
    /// </summary>
    Fail = 3
}
