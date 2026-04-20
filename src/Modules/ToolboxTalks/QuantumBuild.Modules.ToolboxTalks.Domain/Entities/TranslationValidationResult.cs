using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Per-section validation result within a translation validation run.
/// </summary>
public class TranslationValidationResult : BaseEntity
{
    // Foreign key
    public Guid ValidationRunId { get; set; }

    // Section identification
    public int SectionIndex { get; set; }
    public string SectionTitle { get; set; } = string.Empty;

    // Source and translation text
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;

    // Back-translations (multi-round consensus)
    public string? BackTranslationA { get; set; }
    public string? BackTranslationB { get; set; }
    public string? BackTranslationC { get; set; }
    public string? BackTranslationD { get; set; }

    // Scores per back-translation
    public int ScoreA { get; set; }
    public int ScoreB { get; set; }
    public int? ScoreC { get; set; }
    public int? ScoreD { get; set; }

    // Consensus result
    public int FinalScore { get; set; }
    public int RoundsUsed { get; set; }
    public ValidationOutcome Outcome { get; set; }
    public ValidationOutcome EngineOutcome { get; set; }

    // Safety-critical metadata
    public bool IsSafetyCritical { get; set; }
    public string? CriticalTerms { get; set; }
    public string? GlossaryMismatches { get; set; }
    public int EffectiveThreshold { get; set; }

    // Translation quality diagnostics
    public string? ArtefactsJson { get; set; }
    public string? RegistryViolationsJson { get; set; }
    public string? ReviewReasonsJson { get; set; }

    // Glossary hard-block corrections applied before consensus scoring
    public string? GlossaryCorrectionsJson { get; set; }
    public bool? GlossaryHardBlockApplied { get; set; }

    // Reviewer decision
    public ReviewerDecision ReviewerDecision { get; set; } = ReviewerDecision.Pending;
    public string? EditedTranslation { get; set; }
    public DateTime? DecisionAt { get; set; }
    public string? DecisionBy { get; set; }

    // Navigation property
    public TranslationValidationRun ValidationRun { get; set; } = null!;
}
