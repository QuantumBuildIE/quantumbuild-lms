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
}
