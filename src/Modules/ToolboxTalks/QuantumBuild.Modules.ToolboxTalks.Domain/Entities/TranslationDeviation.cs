using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

/// <summary>
/// Tenant-scoped record of a translation quality deviation found during review.
/// Deviations are the primary audit mechanism for tracking what went wrong,
/// why it happened, and what corrective/preventive actions were taken.
/// Sequential DeviationId per tenant: "DEV-001", "DEV-002", etc.
/// </summary>
public class TranslationDeviation : TenantEntity
{
    /// <summary>Sequential tenant-scoped identifier, e.g. "DEV-001".</summary>
    public string DeviationId { get; set; } = string.Empty;

    /// <summary>When the deviation was detected.</summary>
    public DateTimeOffset DetectedAt { get; set; }

    /// <summary>Username or free-text name of the person who detected this deviation.</summary>
    public string DetectedBy { get; set; } = string.Empty;

    // --- Affected content ---

    /// <summary>FK to the validation run this deviation relates to (nullable — may be standalone).</summary>
    public Guid? ValidationRunId { get; set; }
    public TranslationValidationRun? ValidationRun { get; set; }

    /// <summary>FK to the specific section result (nullable — may affect multiple sections).</summary>
    public Guid? ValidationResultId { get; set; }

    /// <summary>Snapshot of the talk or course title at time of deviation.</summary>
    public string? ModuleRef { get; set; }

    /// <summary>Snapshot of the section title at time of deviation.</summary>
    public string? LessonRef { get; set; }

    /// <summary>Language pair, e.g. "en-pl".</summary>
    public string? LanguagePair { get; set; }

    // --- What went wrong ---

    /// <summary>Excerpt from the source (original) text where the issue occurs.</summary>
    public string? SourceExcerpt { get; set; }

    /// <summary>Excerpt from the target (translated) text that demonstrates the issue.</summary>
    public string? TargetExcerpt { get; set; }

    /// <summary>Free-text description of what went wrong.</summary>
    public string Nature { get; set; } = string.Empty;

    /// <summary>Category of root cause: terminology | fluency | safety | completeness | pipeline | other.</summary>
    public string RootCauseCategory { get; set; } = string.Empty;

    /// <summary>Additional detail on the root cause.</summary>
    public string? RootCauseDetail { get; set; }

    // --- Response ---

    /// <summary>What was done to correct this specific occurrence.</summary>
    public string? CorrectiveAction { get; set; }

    /// <summary>What was done to prevent recurrence.</summary>
    public string? PreventiveAction { get; set; }

    /// <summary>Name of the approver who signed off on the corrective/preventive actions.</summary>
    public string? Approver { get; set; }

    // --- Status ---

    public DeviationStatus Status { get; set; } = DeviationStatus.Open;

    public string? ClosedBy { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }

    // --- Pipeline context ---

    /// <summary>Hash snapshot of the active pipeline version when this deviation was detected.</summary>
    public string? PipelineVersionAtTime { get; set; }
}
