using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;

/// <summary>
/// Provider for accessing video files from different sources (Google Drive, Azure Blob, etc.).
/// Used to get direct URLs for video processing.
/// </summary>
public interface IVideoSourceProvider
{
    /// <summary>
    /// Whether this provider supports video uploads.
    /// </summary>
    bool SupportsUpload { get; }

    /// <summary>
    /// Gets a direct URL to a video file that can be used for transcription.
    /// May convert share links (e.g., Google Drive) to direct download URLs.
    /// </summary>
    /// <param name="sourceUrl">Original video URL or share link</param>
    /// <param name="sourceType">Type of video source</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with direct URL</returns>
    Task<VideoSourceResult> GetDirectUrlAsync(
        string sourceUrl,
        SubtitleVideoSourceType sourceType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a video to storage (if supported).
    /// </summary>
    /// <param name="videoStream">Video file stream</param>
    /// <param name="fileName">Desired file name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result with URL</returns>
    Task<VideoUploadResult> UploadVideoAsync(
        Stream videoStream,
        string fileName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of getting a direct video URL
/// </summary>
public class VideoSourceResult
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Direct URL to the video file
    /// </summary>
    public string? DirectUrl { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static VideoSourceResult SuccessResult(string directUrl) =>
        new()
        {
            Success = true,
            DirectUrl = directUrl
        };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static VideoSourceResult FailureResult(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}

/// <summary>
/// Result of uploading a video
/// </summary>
public class VideoUploadResult
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
    /// URL to access the uploaded video
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Final file name (may differ from requested if renamed)
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static VideoUploadResult SuccessResult(string url, string fileName) =>
        new()
        {
            Success = true,
            Url = url,
            FileName = fileName
        };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static VideoUploadResult FailureResult(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
