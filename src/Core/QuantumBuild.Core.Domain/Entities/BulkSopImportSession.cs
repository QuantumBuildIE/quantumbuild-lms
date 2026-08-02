using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Core.Domain.Enums;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Tracks a single bulk SOP-to-learning import from ZIP upload through to per-PDF learning
/// creation. Mirrors BulkImportSession's upload -> validate -> confirm -> background-process
/// -> poll shape.
/// </summary>
public class BulkSopImportSession : TenantEntity
{
    /// <summary>R2 object key of the uploaded ZIP (e.g. "{tenantId}/sessions/{id}/upload.zip").</summary>
    public string ZipR2Key { get; set; } = string.Empty;

    public BulkSopImportStatus Status { get; set; } = BulkSopImportStatus.Uploaded;

    /// <summary>When the ZIP was uploaded. Distinct from CreatedAt for future cleanup queries.</summary>
    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>
    /// JSON-serialised BulkSopImportValidationResult. Null until the validation pass completes;
    /// set when Status transitions to Validated. The Hangfire job deserialises this to determine
    /// which ZIP entries to process.
    /// </summary>
    public string? ValidationResultJson { get; set; }

    /// <summary>
    /// Set by the Hangfire job the moment it transitions the session to Processing.
    /// Null means the job has never started. Used to detect sessions stuck in Processing
    /// after a mid-job process restart so the confirm endpoint can allow re-triggering.
    /// </summary>
    public DateTimeOffset? ProcessingStartedAt { get; set; }

    /// <summary>
    /// JSON-serialised BulkSopImportProcessingResult. Null until the job runs.
    /// Set when Status transitions to Completed or Failed (partial results are preserved
    /// so the admin can see what succeeded before a job-level failure).
    /// </summary>
    public string? ProcessingResultJson { get; set; }

    /// <summary>
    /// True when this session was re-triggered via the stuck-session recovery path.
    /// When true, the Hangfire job treats a title-uniqueness conflict as evidence the item
    /// already succeeded in the interrupted first run (AlreadyExisted) rather than a real failure.
    /// </summary>
    public bool IsRerun { get; set; } = false;
}
