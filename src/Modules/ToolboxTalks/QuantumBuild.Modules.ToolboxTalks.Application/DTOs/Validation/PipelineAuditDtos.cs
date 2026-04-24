using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

// ─── Deviation DTOs ───────────────────────────────────────────────────────────

public record TranslationDeviationDto
{
    public Guid Id { get; init; }
    public string DeviationId { get; init; } = string.Empty;
    public DateTimeOffset DetectedAt { get; init; }
    public string DetectedBy { get; init; } = string.Empty;
    public Guid? ValidationRunId { get; init; }
    public Guid? ValidationResultId { get; init; }
    public string? ModuleRef { get; init; }
    public string? LessonRef { get; init; }
    public string? LanguagePair { get; init; }
    public string? SourceExcerpt { get; init; }
    public string? TargetExcerpt { get; init; }
    public string Nature { get; init; } = string.Empty;
    public string RootCauseCategory { get; init; } = string.Empty;
    public string? RootCauseDetail { get; init; }
    public string? CorrectiveAction { get; init; }
    public string? PreventiveAction { get; init; }
    public string? Approver { get; init; }
    public DeviationStatus Status { get; init; }
    public string? ClosedBy { get; init; }
    public DateTimeOffset? ClosedAt { get; init; }
    public string? PipelineVersionAtTime { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public record CreateDeviationRequest
{
    public string DetectedBy { get; init; } = string.Empty;
    public Guid? ValidationRunId { get; init; }
    public Guid? ValidationResultId { get; init; }
    public string? ModuleRef { get; init; }
    public string? LessonRef { get; init; }
    public string? LanguagePair { get; init; }
    public string? SourceExcerpt { get; init; }
    public string? TargetExcerpt { get; init; }
    public string Nature { get; init; } = string.Empty;
    public string RootCauseCategory { get; init; } = string.Empty;
    public string? RootCauseDetail { get; init; }
    public string? CorrectiveAction { get; init; }
    public string? PreventiveAction { get; init; }
    public string? Approver { get; init; }
}

public record UpdateDeviationStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? ClosedBy { get; init; }
}

public record DeviationSummaryDto
{
    public int OpenCount { get; init; }
    public int InProgressCount { get; init; }
    public int ClosedCount { get; init; }
    public int Total { get; init; }
}

// ─── Pipeline Change Record DTOs ─────────────────────────────────────────────

public record PipelineChangeRecordDto
{
    public Guid Id { get; init; }
    public string ChangeId { get; init; } = string.Empty;
    public string Component { get; init; } = string.Empty;
    public string ChangeFrom { get; init; } = string.Empty;
    public string ChangeTo { get; init; } = string.Empty;
    public string Justification { get; init; } = string.Empty;
    public string? ImpactAssessment { get; init; }
    public string? PriorModulesAction { get; init; }
    public string? Approver { get; init; }
    public DateTimeOffset DeployedAt { get; init; }
    public Guid PipelineVersionId { get; init; }
    public string? PipelineVersionHash { get; init; }
    public string? PipelineVersionLabel { get; init; }
    public Guid? PreviousPipelineVersionId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public record CreatePipelineChangeRecordRequest
{
    public string Component { get; init; } = string.Empty;
    public string ChangeFrom { get; init; } = string.Empty;
    public string ChangeTo { get; init; } = string.Empty;
    public string Justification { get; init; } = string.Empty;
    public string? ImpactAssessment { get; init; }
    public string? PriorModulesAction { get; init; }
    public string? Approver { get; init; }
    /// <summary>Version label for the new pipeline version this change produces.</summary>
    public string NewVersionLabel { get; init; } = string.Empty;
}

// ─── Module Outcome DTOs ─────────────────────────────────────────────────────

public record ModuleOutcomeDto
{
    public Guid RunId { get; init; }
    public Guid? ToolboxTalkId { get; init; }
    public string? TalkTitle { get; init; }
    public Guid? CourseId { get; init; }
    public string? CourseTitle { get; init; }
    public string LanguageCode { get; init; } = string.Empty;
    public string? SectorKey { get; init; }
    public double OverallScore { get; init; }
    public ValidationOutcome OverallOutcome { get; init; }
    public bool IsSafetyCritical { get; init; }
    public int TotalSections { get; init; }
    public int PassedSections { get; init; }
    public int ReviewSections { get; init; }
    public int FailedSections { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? PipelineVersionHash { get; init; }
    public int AcceptedDecisions { get; init; }
    public int RejectedDecisions { get; init; }
    public int PendingDecisions { get; init; }
}

// ─── Dashboard DTO ───────────────────────────────────────────────────────────

public record PipelineAuditDashboardDto
{
    public int OpenDeviations { get; init; }
    public int InProgressDeviations { get; init; }
    public int ClosedDeviations { get; init; }
    public int ChangeRecords { get; init; }
    public int LockedTerms { get; init; }
    public int ModuleOutcomes { get; init; }
    public string ActivePipelineVersion { get; init; } = string.Empty;
    public string ActivePipelineHash { get; init; } = string.Empty;
    public PipelineChangeRecordDto? MostRecentChangeRecord { get; init; }
    public IReadOnlyList<TranslationDeviationDto> TopOpenDeviations { get; init; } = [];
}

// ─── Term Gate DTOs ──────────────────────────────────────────────────────────

public record TermGateCheckRequest
{
    public string SourceText { get; init; } = string.Empty;
    public string TargetText { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = string.Empty;
    public string SectorKey { get; init; } = string.Empty;
}

public record TermGatePassingTerm
{
    public Guid TermId { get; init; }
    public string EnglishTerm { get; init; } = string.Empty;
    public string ApprovedTranslation { get; init; } = string.Empty;
}

public record TermGateFailure
{
    public Guid TermId { get; init; }
    public string EnglishTerm { get; init; } = string.Empty;
    public string ExpectedTranslation { get; init; } = string.Empty;
    /// <summary>"missing_approved" or "forbidden_present"</summary>
    public string FailureReason { get; init; } = string.Empty;
    public string? ForbiddenTermFound { get; init; }
}

public record TermGateCheckResult
{
    public bool Passed { get; init; }
    public int CheckedCount { get; init; }
    public IReadOnlyList<TermGateFailure> Failures { get; init; } = [];
    public IReadOnlyList<TermGatePassingTerm> PassingTerms { get; init; } = [];
}

public record TermGateSectorSummary
{
    public string SectorKey { get; init; } = string.Empty;
    public string SectorName { get; init; } = string.Empty;
    public int TermCount { get; init; }
}

public record TermGateSummaryDto
{
    public int TotalTerms { get; init; }
    public int CriticalTerms { get; init; }
    public IReadOnlyList<TermGateSectorSummary> TermsBySector { get; init; } = [];
    /// <summary>Language codes (e.g. "pl", "fr") that have at least one approved translation in the effective glossary.</summary>
    public IReadOnlyList<string> LanguagesWithCoverage { get; init; } = [];
}
