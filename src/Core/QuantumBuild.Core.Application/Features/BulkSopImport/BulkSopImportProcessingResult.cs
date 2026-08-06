namespace QuantumBuild.Core.Application.Features.BulkSopImport;

public enum BulkSopImportItemStatus
{
    Succeeded = 1,
    Failed = 2,

    /// <summary>
    /// Set on re-run sessions only: a learning with this item's derived title already exists,
    /// which means it was created successfully by the interrupted first run. Not a real failure.
    /// </summary>
    AlreadyExisted = 3
}

/// <summary>
/// Top-level result produced by BulkSopImportJob.
/// Serialised to BulkSopImportSession.ProcessingResultJson when the job finishes.
/// </summary>
public sealed record BulkSopImportProcessingResult
{
    public int TotalAttempted { get; init; }
    public int SucceededCount { get; init; }
    public int FailedCount { get; init; }

    /// <summary>Non-zero only when IsRerun is true — see BulkSopImportItemStatus.AlreadyExisted.</summary>
    public int AlreadyExistedCount { get; init; }

    public List<BulkSopImportItemOutcome> Items { get; init; } = new();
}

/// <summary>Per-PDF outcome from the Hangfire job. One entry per queued file.</summary>
public sealed record BulkSopImportItemOutcome
{
    public int ItemIndex { get; init; }
    public string FileName { get; init; } = string.Empty;
    public BulkSopImportItemStatus Status { get; init; }

    /// <summary>Set when Status is Succeeded or AlreadyExisted.</summary>
    public Guid? ToolboxTalkId { get; init; }

    /// <summary>Set when Status is Succeeded or AlreadyExisted.</summary>
    public string? ToolboxTalkTitle { get; init; }

    /// <summary>Set when Status is Failed. Human-readable reason for the failure.</summary>
    public string? FailureReason { get; init; }

    /// <summary>
    /// Set when Status is Succeeded but quiz generation failed after sections were created
    /// successfully — the learning was still created as a valid Draft (sections only, no quiz),
    /// consistent with a wizard user stopping after the Parse step. Null on a clean success.
    /// </summary>
    public string? Warning { get; init; }
}
