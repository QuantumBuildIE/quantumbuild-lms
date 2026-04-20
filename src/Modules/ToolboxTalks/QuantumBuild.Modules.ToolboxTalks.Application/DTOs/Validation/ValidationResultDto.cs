using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// DTO for a single section's validation result
/// </summary>
public record ValidationResultDto
{
    public Guid Id { get; init; }
    public int SectionIndex { get; init; }
    public string SectionTitle { get; init; } = string.Empty;
    public string OriginalText { get; init; } = string.Empty;
    public string TranslatedText { get; init; } = string.Empty;

    // Back-translations
    public string? BackTranslationA { get; init; }
    public string? BackTranslationB { get; init; }
    public string? BackTranslationC { get; init; }
    public string? BackTranslationD { get; init; }

    // Scores
    public int ScoreA { get; init; }
    public int ScoreB { get; init; }
    public int? ScoreC { get; init; }
    public int? ScoreD { get; init; }
    public int FinalScore { get; init; }
    public int RoundsUsed { get; init; }
    public ValidationOutcome Outcome { get; init; }
    public ValidationOutcome EngineOutcome { get; init; }

    // Safety
    public bool IsSafetyCritical { get; init; }
    public string? CriticalTerms { get; init; }
    public string? GlossaryMismatches { get; init; }
    public int EffectiveThreshold { get; init; }

    // Translation quality diagnostics
    public string? ArtefactsJson { get; init; }
    public string? RegistryViolationsJson { get; init; }
    public string? ReviewReasonsJson { get; init; }
    public string? GlossaryCorrectionsJson { get; init; }
    public bool? GlossaryHardBlockApplied { get; init; }

    // Reviewer decision
    public ReviewerDecision ReviewerDecision { get; init; }
    public string? EditedTranslation { get; init; }
    public DateTime? DecisionAt { get; init; }
    public string? DecisionBy { get; init; }
}
