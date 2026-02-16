using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;

/// <summary>
/// Video source provider for Google Drive and direct URL videos.
/// Converts Google Drive share URLs to direct download URLs for transcription.
/// </summary>
public class GoogleDriveVideoSourceProvider : IVideoSourceProvider
{
    private readonly ILogger<GoogleDriveVideoSourceProvider> _logger;

    public GoogleDriveVideoSourceProvider(ILogger<GoogleDriveVideoSourceProvider> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Google Drive provider does not support video uploads.
    /// </summary>
    public bool SupportsUpload => false;

    /// <summary>
    /// Gets a direct URL to a video file.
    /// Converts Google Drive share links to direct download URLs.
    /// </summary>
    public Task<VideoSourceResult> GetDirectUrlAsync(
        string sourceUrl,
        SubtitleVideoSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (sourceType == SubtitleVideoSourceType.DirectUrl)
            {
                return Task.FromResult(VideoSourceResult.SuccessResult(sourceUrl));
            }

            if (sourceType != SubtitleVideoSourceType.GoogleDrive)
            {
                return Task.FromResult(VideoSourceResult.FailureResult(
                    $"Unsupported video source type: {sourceType}. Use Azure Blob provider for Azure Blob Storage videos."));
            }

            var directUrl = ConvertGoogleDriveUrl(sourceUrl);

            if (string.IsNullOrEmpty(directUrl))
            {
                return Task.FromResult(VideoSourceResult.FailureResult(
                    "Could not extract Google Drive file ID from URL. " +
                    "Supported formats: https://drive.google.com/file/d/{fileId}/view or https://drive.google.com/open?id={fileId}"));
            }

            _logger.LogInformation("Converted Google Drive URL to direct download: {Url}", directUrl);

            return Task.FromResult(VideoSourceResult.SuccessResult(directUrl));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to convert video URL: {Url}", sourceUrl);
            return Task.FromResult(VideoSourceResult.FailureResult($"Failed to convert URL: {ex.Message}"));
        }
    }

    /// <summary>
    /// Uploads are not supported by this provider.
    /// </summary>
    public Task<VideoUploadResult> UploadVideoAsync(
        Stream videoStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(VideoUploadResult.FailureResult(
            "Google Drive provider does not support video uploads. Use Azure Blob provider instead."));
    }

    /// <summary>
    /// Converts Google Drive share URL to direct download URL.
    /// Supports formats:
    /// - https://drive.google.com/file/d/{fileId}/view
    /// - https://drive.google.com/open?id={fileId}
    /// </summary>
    private static string? ConvertGoogleDriveUrl(string shareUrl)
    {
        string? fileId = null;

        // Format: /file/d/{fileId}/
        if (shareUrl.Contains("/file/d/"))
        {
            var startIndex = shareUrl.IndexOf("/file/d/", StringComparison.Ordinal) + 8;
            var endIndex = shareUrl.IndexOf('/', startIndex);

            if (endIndex == -1)
                endIndex = shareUrl.IndexOf('?', startIndex);
            if (endIndex == -1)
                endIndex = shareUrl.Length;

            fileId = shareUrl[startIndex..endIndex];
        }
        // Format: ?id={fileId}
        else if (shareUrl.Contains("id="))
        {
            var startIndex = shareUrl.IndexOf("id=", StringComparison.Ordinal) + 3;
            var endIndex = shareUrl.IndexOf('&', startIndex);

            if (endIndex == -1)
                endIndex = shareUrl.Length;

            fileId = shareUrl[startIndex..endIndex];
        }

        if (string.IsNullOrEmpty(fileId))
            return null;

        return $"https://drive.google.com/uc?export=download&id={fileId}";
    }
}
