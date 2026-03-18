namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Status of a regulatory requirement mapping to a talk or course
/// </summary>
public enum RequirementMappingStatus
{
    /// <summary>
    /// AI-suggested mapping awaiting human review
    /// </summary>
    Suggested = 1,

    /// <summary>
    /// Mapping confirmed by a reviewer
    /// </summary>
    Confirmed = 2,

    /// <summary>
    /// Mapping rejected by a reviewer
    /// </summary>
    Rejected = 3
}
