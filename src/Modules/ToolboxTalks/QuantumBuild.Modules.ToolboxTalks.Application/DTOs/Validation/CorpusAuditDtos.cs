using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

// ─── Corpus DTOs ──────────────────────────────────────────────────────────────

public record AuditCorpusDto
{
    public Guid Id { get; init; }
    public string CorpusId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string SectorKey { get; init; } = string.Empty;
    public string LanguagePair { get; init; } = string.Empty;
    public Guid? SourceTalkId { get; init; }
    public string? SourceTalkTitle { get; init; }
    public Guid? FrozenFromPipelineVersionId { get; init; }
    public string? FrozenFromPipelineHash { get; init; }
    public bool IsLocked { get; init; }
    public DateTimeOffset? LockedAt { get; init; }
    public string? LockedBy { get; init; }
    public string? SignedBy { get; init; }
    public int Version { get; init; }
    public int EntryCount { get; init; }
    public int ActiveEntryCount { get; init; }
    public CorpusRunSummaryDto? LastRun { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record AuditCorpusEntryDto
{
    public Guid Id { get; init; }
    public string EntryRef { get; init; } = string.Empty;
    public string SectionTitle { get; init; } = string.Empty;
    public string OriginalText { get; init; } = string.Empty;
    public string TranslatedText { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = string.Empty;
    public string TargetLanguage { get; init; } = string.Empty;
    public string SectorKey { get; init; } = string.Empty;
    public int PassThreshold { get; init; }
    public ValidationOutcome ExpectedOutcome { get; init; }
    public bool IsSafetyCritical { get; init; }
    public Guid? PipelineVersionIdAtFreeze { get; init; }
    public string? TagsJson { get; init; }
    public bool IsActive { get; init; }
}

public record CorpusRunSummaryDto
{
    public Guid Id { get; init; }
    public CorpusRunStatus Status { get; init; }
    public CorpusTriggerType TriggerType { get; init; }
    public string? TriggeredBy { get; init; }
    public bool IsSmokeTest { get; init; }
    public int TotalEntries { get; init; }
    public int RegressionEntries { get; init; }
    public decimal? MeanScore { get; init; }
    public int? MaxScoreDrop { get; init; }
    public CorpusVerdict? Verdict { get; init; }
    public decimal? EstimatedCostEur { get; init; }
    public decimal? ActualCostEur { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string? PipelineVersionHash { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CorpusRunDetailDto
{
    public Guid Id { get; init; }
    public Guid CorpusId { get; init; }
    public string CorpusName { get; init; } = string.Empty;
    public CorpusRunStatus Status { get; init; }
    public CorpusTriggerType TriggerType { get; init; }
    public string? TriggeredBy { get; init; }
    public bool IsSmokeTest { get; init; }
    public int TotalEntries { get; init; }
    public int PassedEntries { get; init; }
    public int ReviewEntries { get; init; }
    public int FailedEntries { get; init; }
    public int RegressionEntries { get; init; }
    public decimal? MeanScore { get; init; }
    public int? MaxScoreDrop { get; init; }
    public CorpusVerdict? Verdict { get; init; }
    public int FailureThresholdPercent { get; init; }
    public int ScoreDropThreshold { get; init; }
    public decimal? EstimatedCostEur { get; init; }
    public decimal? ActualCostEur { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public Guid? PipelineVersionId { get; init; }
    public string? PipelineVersionHash { get; init; }
    public Guid? LinkedPipelineChangeId { get; init; }
    public string? LinkedPipelineChangeStatus { get; init; }
    public IReadOnlyList<CorpusRunResultDto> Results { get; init; } = [];
}

public record CorpusRunResultDto
{
    public Guid Id { get; init; }
    public Guid CorpusEntryId { get; init; }
    public string EntryRef { get; init; } = string.Empty;
    public string SectionTitle { get; init; } = string.Empty;
    public int FinalScore { get; init; }
    public ValidationOutcome Outcome { get; init; }
    public ValidationOutcome ExpectedOutcome { get; init; }
    public bool IsRegression { get; init; }
    public int? ScoreDelta { get; init; }
    public int RoundsUsed { get; init; }
    public bool IsSafetyCritical { get; init; }
    public int EffectiveThreshold { get; init; }
    public string? BackTranslationA { get; init; }
    public string? BackTranslationB { get; init; }
    public string? BackTranslationC { get; init; }
    public string? BackTranslationD { get; init; }
    public int? ScoreA { get; init; }
    public int? ScoreB { get; init; }
    public int? ScoreC { get; init; }
    public int? ScoreD { get; init; }
    public string? GlossaryCorrectionsJson { get; init; }
    public string? ArtefactsJson { get; init; }
    public string? ReviewReasonsJson { get; init; }
    public bool WasCached { get; init; }
}

public record CorpusRunDiffDto
{
    public Guid RunId { get; init; }
    public Guid? PreviousRunId { get; init; }
    public IReadOnlyList<CorpusRunDiffEntry> RegressionEntries { get; init; } = [];
}

public record CorpusRunDiffEntry
{
    public Guid CorpusEntryId { get; init; }
    public string EntryRef { get; init; } = string.Empty;
    public string SectionTitle { get; init; } = string.Empty;
    public string TranslatedText { get; init; } = string.Empty;
    public int CurrentScore { get; init; }
    public ValidationOutcome CurrentOutcome { get; init; }
    public int? PreviousScore { get; init; }
    public ValidationOutcome? PreviousOutcome { get; init; }
    public int? ScoreDelta { get; init; }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record FreezeCorpusRequest
{
    public Guid TalkId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<int> SectionIndexes { get; init; } = [];
}

public record LockCorpusRequest
{
    public string SignedBy { get; init; } = string.Empty;
}

public record AddCorpusEntryRequest
{
    public string SectionTitle { get; init; } = string.Empty;
    public string OriginalText { get; init; } = string.Empty;
    public string TranslatedText { get; init; } = string.Empty;
    public string SourceLanguage { get; init; } = string.Empty;
    public string TargetLanguage { get; init; } = string.Empty;
    public int PassThreshold { get; init; }
    public ValidationOutcome ExpectedOutcome { get; init; }
    public bool IsSafetyCritical { get; init; }
    public string? TagsJson { get; init; }
}

public record TriggerCorpusRunRequest
{
    public bool IsSmokeTest { get; init; }
}

public record ConfirmCorpusRunRequest
{
    public Guid CorpusRunId { get; init; }
}

public record TriggerCorpusRunResponse
{
    public Guid CorpusRunId { get; init; }
    public decimal EstimatedCostEur { get; init; }
    public bool RequiresConfirmation { get; init; }
    public bool RequiresSuperUserApproval { get; init; }
}

// ─── Pipeline Change Status DTOs ─────────────────────────────────────────────

public record UpdateChangeStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? Justification { get; init; }
}
