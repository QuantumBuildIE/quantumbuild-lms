namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;

/// <summary>
/// Provider for storing and retrieving SRT subtitle files.
/// Implementations may use GitHub, Azure Blob, or database storage.
/// </summary>
public interface ISrtStorageProvider
{
    /// <summary>
    /// Uploads an SRT file to storage.
    /// </summary>
    /// <param name="srtContent">SRT formatted content to upload</param>
    /// <param name="fileName">File name (e.g., "talk-123-en.srt")</param>
    /// <param name="tenantId">Tenant ID for multi-tenant isolation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result with URL to the stored file</returns>
    Task<SrtUploadResult> UploadSrtAsync(
        string srtContent,
        string fileName,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves SRT content from storage.
    /// </summary>
    /// <param name="fileName">File name to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>SRT content or null if not found</returns>
    Task<string?> GetSrtContentAsync(
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an SRT file from storage.
    /// </summary>
    /// <param name="fileName">File name to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteSrtAsync(
        string fileName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an SRT upload operation
/// </summary>
public class SrtUploadResult
{
    /// <summary>
    /// Whether the upload was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if upload failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Public URL to access the uploaded SRT file
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static SrtUploadResult SuccessResult(string url) =>
        new()
        {
            Success = true,
            Url = url
        };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static SrtUploadResult FailureResult(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
