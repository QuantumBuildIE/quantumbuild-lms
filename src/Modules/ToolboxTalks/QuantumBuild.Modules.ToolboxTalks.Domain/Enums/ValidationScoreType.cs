namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Type of regulatory score assessment
/// </summary>
public enum ValidationScoreType
{
    /// <summary>
    /// Scores the source document itself against regulatory standards
    /// </summary>
    SourceDocument = 1,

    /// <summary>
    /// Pure linguistic translation quality — no regulatory overlay
    /// </summary>
    PureTranslation = 2,

    /// <summary>
    /// Translation scored against sector-specific regulatory criteria
    /// </summary>
    RegulatoryTranslation = 3
}
