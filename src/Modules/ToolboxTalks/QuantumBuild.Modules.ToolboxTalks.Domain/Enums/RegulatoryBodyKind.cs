namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Distinguishes legally-mandated regulations from voluntarily-adopted standards.
/// </summary>
public enum RegulatoryBodyKind
{
    /// <summary>
    /// Legally mandated — applies to tenants automatically via sector (RegulatoryProfile chain)
    /// </summary>
    Regulation = 1,

    /// <summary>
    /// Voluntarily adopted — tenants explicitly subscribe (TenantStandardSubscription)
    /// </summary>
    Standard = 2
}
