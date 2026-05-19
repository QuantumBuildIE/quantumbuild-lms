namespace QuantumBuild.Core.Application.Features.BulkImport;

/// <summary>
/// Parses and validates a bulk employee import CSV without creating any records.
/// Accepts a raw stream so it is testable without the R2 upload endpoint (Stage 4).
/// </summary>
public interface IBulkEmployeeImportValidationService
{
    /// <summary>
    /// Reads <paramref name="csvStream"/>, validates every row against the target tenant's
    /// existing employees and global user accounts, and returns a structured result.
    /// The caller is responsible for disposing the stream.
    /// </summary>
    /// <remarks>
    /// Employee uniqueness is checked against the tenant resolved from ICurrentUserService.
    /// Stage 2: when called from a Hangfire job, ensure the DbContext tenant filter is set
    /// correctly (e.g. via a scoped ICurrentUserService override) before invoking this method.
    /// </remarks>
    Task<BulkImportValidationResult> ValidateAsync(
        Stream csvStream,
        CancellationToken cancellationToken = default);
}
