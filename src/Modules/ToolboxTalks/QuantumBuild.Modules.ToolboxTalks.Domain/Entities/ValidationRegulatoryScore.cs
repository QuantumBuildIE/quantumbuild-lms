using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Persists a regulatory score assessment for a translation validation run.
/// Three score types: SourceDocument (source quality), PureTranslation (linguistic),
/// RegulatoryTranslation (sector-specific regulatory compliance).
/// </summary>
public class ValidationRegulatoryScore : TenantEntity
{
    public Guid ValidationRunId { get; set; }

    public ValidationScoreType ScoreType { get; set; }

    /// <summary>
    /// Nullable FK to RegulatoryProfile — null for PureTranslation type
    /// </summary>
    public Guid? RegulatoryProfileId { get; set; }

    /// <summary>
    /// Overall score 0–100
    /// </summary>
    public int OverallScore { get; set; }

    /// <summary>
    /// JSON array of {Key, Label, Weight, Score} objects
    /// </summary>
    public string CategoryScoresJson { get; set; } = "[]";

    public string Verdict { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// e.g. "Pre-Remediation Baseline", "Post-Remediation Pass 2"
    /// </summary>
    public string RunLabel { get; set; } = string.Empty;

    /// <summary>
    /// Sequential per {ValidationRunId, ScoreType}
    /// </summary>
    public int RunNumber { get; set; }

    /// <summary>
    /// Full Claude response stored for audit/debugging
    /// </summary>
    public string FullResponseJson { get; set; } = string.Empty;

    public int ScoredSectionCount { get; set; }

    public string TargetLanguage { get; set; } = string.Empty;

    /// <summary>
    /// Denormalised copy of RegulatoryBody.Code e.g. "HIQA"
    /// </summary>
    public string? RegulatoryBody { get; set; }

    // Navigation properties
    public TranslationValidationRun ValidationRun { get; set; } = null!;
    public RegulatoryProfile? RegulatoryProfile { get; set; }
}
