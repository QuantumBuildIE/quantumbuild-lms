namespace QuantumBuild.Core.Application.Features.BulkImport;

public enum BulkImportRowOutcomeStatus
{
    Created = 1,
    Failed = 2
}

/// <summary>
/// Top-level result produced by BulkEmployeeImportJob.
/// Serialised to BulkImportSession.ProcessingResultJson when the job finishes.
/// Stage 3: the admin UI deserialises this to show per-row outcome.
/// </summary>
public sealed record BulkImportProcessingResult
{
    public int TotalAttempted { get; init; }
    public int CreatedCount { get; init; }
    public int FailedCount { get; init; }

    /// <summary>
    /// Number of invitation emails successfully sent (subset of CreatedCount where
    /// CreateUserAccount was true).
    /// </summary>
    public int InvitationEmailsSent { get; init; }

    public List<BulkImportRowOutcome> Rows { get; init; } = new();
}

/// <summary>
/// Per-row outcome from the Stage 2 job. One entry per row that was attempted
/// (Valid and Warning rows only; Failed validation rows are skipped and not represented here).
/// </summary>
public sealed record BulkImportRowOutcome
{
    /// <summary>1-based row number matching the original spreadsheet.</summary>
    public int RowNumber { get; init; }

    public BulkImportRowOutcomeStatus Status { get; init; }

    /// <summary>Set when Status is Created.</summary>
    public Guid? EmployeeId { get; init; }

    /// <summary>Set when Status is Created and a user account was created.</summary>
    public Guid? LinkedUserId { get; init; }

    /// <summary>True when Status is Created, CreateUserAccount was true, and the invitation email was sent.</summary>
    public bool InvitationEmailSent { get; init; }

    /// <summary>Set when Status is Failed. Human-readable reason for the failure.</summary>
    public string? FailureReason { get; init; }
}
