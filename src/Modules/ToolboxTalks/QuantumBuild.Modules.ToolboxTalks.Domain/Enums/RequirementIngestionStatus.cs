namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Approval status of an ingested regulatory requirement
/// </summary>
public enum RequirementIngestionStatus
{
    /// <summary>
    /// Requirement is awaiting review
    /// </summary>
    Draft = 1,

    /// <summary>
    /// Requirement has been approved for use
    /// </summary>
    Approved = 2,

    /// <summary>
    /// Requirement was rejected during review
    /// </summary>
    Rejected = 3
}
