namespace QuantumBuild.Core.Application.Features.BulkSopImport;

/// <summary>
/// Inspects a ZIP archive of SOP PDFs without creating any records or uploading anything.
/// Accepts a raw stream so it is testable independently of the upload endpoint.
/// </summary>
public interface IBulkSopImportValidationService
{
    /// <summary>
    /// Reads <paramref name="zipStream"/>, classifies each entry as a queued PDF or an ignored
    /// (non-PDF/junk/directory) entry, and enforces file-count and total-size limits.
    /// The caller is responsible for disposing the stream.
    /// </summary>
    Task<BulkSopImportValidationResult> ValidateAsync(
        Stream zipStream,
        CancellationToken cancellationToken = default);
}
