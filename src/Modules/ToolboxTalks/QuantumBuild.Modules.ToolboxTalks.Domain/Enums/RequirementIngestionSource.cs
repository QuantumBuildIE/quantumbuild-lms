namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// How a regulatory requirement was ingested into the system
/// </summary>
public enum RequirementIngestionSource
{
    /// <summary>
    /// Manually entered by an administrator
    /// </summary>
    Manual = 1,

    /// <summary>
    /// Automatically extracted via AI ingestion
    /// </summary>
    Automated = 2
}
