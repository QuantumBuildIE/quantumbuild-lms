using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Per-entry result within a CorpusRun. Mirrors TranslationValidationResult fields
/// that are relevant to corpus regression testing.
/// </summary>
public class CorpusRunResult : BaseEntity
{
    public Guid CorpusRunId { get; set; }
    public CorpusRun CorpusRun { get; set; } = null!;

    public Guid CorpusEntryId { get; set; }
    public AuditCorpusEntry CorpusEntry { get; set; } = null!;

    public int FinalScore { get; set; }

    public ValidationOutcome Outcome { get; set; }

    /// <summary>Expected outcome copied from the entry at run time.</summary>
    public ValidationOutcome ExpectedOutcome { get; set; }

    /// <summary>True if Outcome is worse than ExpectedOutcome.</summary>
    public bool IsRegression { get; set; }

    /// <summary>Score difference vs previous run on the same entry (positive = improved).</summary>
    public int? ScoreDelta { get; set; }

    public int RoundsUsed { get; set; }

    public bool IsSafetyCritical { get; set; }

    public int EffectiveThreshold { get; set; }

    public string? BackTranslationA { get; set; }
    public string? BackTranslationB { get; set; }
    public string? BackTranslationC { get; set; }
    public string? BackTranslationD { get; set; }

    public int? ScoreA { get; set; }
    public int? ScoreB { get; set; }
    public int? ScoreC { get; set; }
    public int? ScoreD { get; set; }

    public string? GlossaryCorrectionsJson { get; set; }

    public string? ArtefactsJson { get; set; }

    public string? ReviewReasonsJson { get; set; }

    /// <summary>True if any provider result for this entry was served from ProviderResultCache.</summary>
    public bool WasCached { get; set; }
}
