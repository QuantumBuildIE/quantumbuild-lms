using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Core.Domain.Enums;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Tracks a single bulk employee CSV import from upload through to record creation.
/// UploadedAt and Status are indexed so a future cleanup job can locate abandoned sessions
/// without touching ValidationResultJson or other payload columns.
/// </summary>
public class BulkImportSession : TenantEntity
{
    /// <summary>R2 object key of the uploaded CSV file (e.g. "{tenantId}/bulk-import/{id}.csv").</summary>
    public string CsvR2Key { get; set; } = string.Empty;

    public BulkImportStatus Status { get; set; } = BulkImportStatus.Uploaded;

    /// <summary>
    /// When the CSV was uploaded. Distinct from CreatedAt so cleanup queries have an
    /// explicit, semantically named field to filter on (e.g. UploadedAt < now - 24h).
    /// </summary>
    public DateTimeOffset UploadedAt { get; set; }

    /// <summary>
    /// JSON-serialised BulkImportValidationResult. Null until the validation pass completes;
    /// set when Status transitions to Validated (or Failed if the parse itself errors).
    /// Stage 2: the Hangfire job deserialises this to determine which rows to create.
    /// </summary>
    public string? ValidationResultJson { get; set; }

    /// <summary>
    /// Set by the Stage 2 Hangfire job the moment it transitions the session to Processing.
    /// Null means the job has never started.
    /// Stage 4: if ProcessingStartedAt is non-null and now - ProcessingStartedAt exceeds the
    /// Hangfire.PostgreSql InvisibilityTimeout (default 30 min), the session is stuck after
    /// a mid-job process restart and the Stage 4 trigger endpoint should allow re-triggering.
    /// </summary>
    public DateTimeOffset? ProcessingStartedAt { get; set; }

    /// <summary>
    /// JSON-serialised BulkImportProcessingResult. Null until the Stage 2 job runs.
    /// Set when Status transitions to Completed or when the job fails after processing at
    /// least one row (partial results are preserved so the admin can see what succeeded).
    /// </summary>
    public string? ProcessingResultJson { get; set; }
}
