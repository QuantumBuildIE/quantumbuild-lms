using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Storage;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Storage;

/// <summary>
/// Cloudflare R2 storage service with tenant isolation.
/// All files are stored under {tenant-id}/{folder}/{filename} structure.
/// </summary>
public class R2StorageService : IR2StorageService, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly R2StorageSettings _settings;
    private readonly ISlugGeneratorService _slugGenerator;
    private readonly ILogger<R2StorageService> _logger;

    private const string VideosFolder = "videos";
    private const string PdfsFolder = "pdfs";
    private const string SubsFolder = "subs";
    private const string CertificatesFolder = "certificates";

    public R2StorageService(
        IOptions<R2StorageSettings> settings,
        ISlugGeneratorService slugGenerator,
        ILogger<R2StorageService> logger)
    {
        _settings = settings.Value;
        _slugGenerator = slugGenerator;
        _logger = logger;

        var config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = true
        };

        _s3Client = new AmazonS3Client(
            _settings.AccessKeyId,
            _settings.SecretAccessKey,
            config);
    }

    #region Subtitles

    public async Task<R2UploadResult> UploadSubtitleAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string talkTitle,
        string languageCode,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate filename: {slug}_{shortId}_{lang}.srt
            var baseFileName = _slugGenerator.GenerateFileName(talkTitle, toolboxTalkId, "srt");
            var fileName = baseFileName.Replace(".srt", $"_{languageCode.ToLower()}.srt");
            var key = BuildKey(tenantId, SubsFolder, fileName);

            _logger.LogInformation("Uploading subtitle to R2: {Key}", key);

            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = content,
                ContentType = "application/x-subrip",
                // R2-specific settings
                DisablePayloadSigning = true,
                UseChunkEncoding = false
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);

            var publicUrl = GeneratePublicUrl(tenantId, SubsFolder, fileName);

            _logger.LogInformation("Successfully uploaded subtitle: {Url}", publicUrl);

            return R2UploadResult.SuccessResult(publicUrl, key, content.Length, "application/x-subrip");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload subtitle for ToolboxTalk {TalkId}, Language {Lang}",
                toolboxTalkId, languageCode);
            return R2UploadResult.FailureResult($"Subtitle upload failed: {ex.Message}");
        }
    }

    #endregion

    #region Videos

    public async Task<R2UploadResult> UploadVideoAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string talkTitle,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate size
            if (content.Length > _settings.MaxVideoSizeBytes)
            {
                var maxMb = _settings.MaxVideoSizeBytes / 1024 / 1024;
                var actualMb = content.Length / 1024 / 1024;
                return R2UploadResult.FailureResult(
                    $"Video size ({actualMb}MB) exceeds maximum allowed ({maxMb}MB)");
            }

            // Get extension from original filename
            var extension = Path.GetExtension(originalFileName).TrimStart('.').ToLower();
            if (string.IsNullOrEmpty(extension))
                extension = "mp4";

            var fileName = _slugGenerator.GenerateFileName(talkTitle, toolboxTalkId, extension);
            var key = BuildKey(tenantId, VideosFolder, fileName);
            var contentType = GetVideoContentType(extension);

            _logger.LogInformation("Uploading video to R2: {Key} ({SizeMb}MB)",
                key, content.Length / 1024 / 1024);

            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                // R2-specific settings
                DisablePayloadSigning = true,
                UseChunkEncoding = false
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);

            var publicUrl = GeneratePublicUrl(tenantId, VideosFolder, fileName);

            _logger.LogInformation("Successfully uploaded video: {Url}", publicUrl);

            return R2UploadResult.SuccessResult(publicUrl, key, content.Length, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload video for ToolboxTalk {TalkId}", toolboxTalkId);
            return R2UploadResult.FailureResult($"Video upload failed: {ex.Message}");
        }
    }

    #endregion

    #region PDFs

    public async Task<R2UploadResult> UploadPdfAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string talkTitle,
        Stream content,
        string originalFileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate size
            if (content.Length > _settings.MaxPdfSizeBytes)
            {
                var maxMb = _settings.MaxPdfSizeBytes / 1024 / 1024;
                var actualMb = content.Length / 1024 / 1024;
                return R2UploadResult.FailureResult(
                    $"PDF size ({actualMb}MB) exceeds maximum allowed ({maxMb}MB)");
            }

            var fileName = _slugGenerator.GenerateFileName(talkTitle, toolboxTalkId, "pdf");
            var key = BuildKey(tenantId, PdfsFolder, fileName);

            _logger.LogInformation("Uploading PDF to R2: {Key}", key);

            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = content,
                ContentType = "application/pdf",
                // R2-specific settings
                DisablePayloadSigning = true,
                UseChunkEncoding = false
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);

            var publicUrl = GeneratePublicUrl(tenantId, PdfsFolder, fileName);

            _logger.LogInformation("Successfully uploaded PDF: {Url}", publicUrl);

            return R2UploadResult.SuccessResult(publicUrl, key, content.Length, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload PDF for ToolboxTalk {TalkId}", toolboxTalkId);
            return R2UploadResult.FailureResult($"PDF upload failed: {ex.Message}");
        }
    }

    #endregion

    #region Certificates

    public async Task<R2UploadResult> UploadCertificateAsync(
        Guid tenantId,
        string certificateNumber,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fileName = $"{certificateNumber}.pdf";
            var key = BuildKey(tenantId, CertificatesFolder, fileName);

            _logger.LogInformation("Uploading certificate to R2: {Key}", key);

            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = content,
                ContentType = "application/pdf",
                DisablePayloadSigning = true,
                UseChunkEncoding = false
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);

            var publicUrl = GeneratePublicUrl(tenantId, CertificatesFolder, fileName);

            _logger.LogInformation("Successfully uploaded certificate: {Url}", publicUrl);

            return R2UploadResult.SuccessResult(publicUrl, key, content.Length, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload certificate {CertificateNumber}", certificateNumber);
            return R2UploadResult.FailureResult($"Certificate upload failed: {ex.Message}");
        }
    }

    #endregion

    #region Slide Images

    public async Task<R2UploadResult> UploadSlideImageAsync(
        string storagePath,
        byte[] imageBytes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var stream = new MemoryStream(imageBytes);
            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = storagePath,
                InputStream = stream,
                ContentType = "image/png",
                DisablePayloadSigning = true,
                UseChunkEncoding = false
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);

            var publicUrl = $"{_settings.PublicUrl.TrimEnd('/')}/{storagePath}";

            _logger.LogInformation("Successfully uploaded slide image: {Key}", storagePath);

            return R2UploadResult.SuccessResult(publicUrl, storagePath, imageBytes.Length, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload slide image to {Path}", storagePath);
            return R2UploadResult.FailureResult($"Slide image upload failed: {ex.Message}");
        }
    }

    #endregion

    #region Downloads

    public async Task<byte[]?> DownloadFileAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Downloading file from R2: {Path}", path);

            var response = await _s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = path,
            }, cancellationToken);

            using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("File not found in R2: {Path}", path);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file from R2: {Path}", path);
            return null;
        }
    }

    #endregion

    #region Bulk Operations

    public async Task DeleteToolboxTalkFilesAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var shortId = toolboxTalkId.ToString("N")[..8];
            var prefix = $"{tenantId}/";

            _logger.LogInformation("Deleting files for ToolboxTalk {TalkId} with prefix {Prefix}",
                toolboxTalkId, prefix);

            // List all objects in the tenant's space
            var listRequest = new ListObjectsV2Request
            {
                BucketName = _settings.BucketName,
                Prefix = prefix
            };

            var response = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);

            // Filter objects that match this toolbox talk's ID pattern
            var keysToDelete = response.S3Objects
                .Where(obj => obj.Key.Contains($"_{shortId}.") || obj.Key.Contains($"_{shortId}_"))
                .Select(obj => new KeyVersion { Key = obj.Key })
                .ToList();

            if (keysToDelete.Count > 0)
            {
                var deleteRequest = new DeleteObjectsRequest
                {
                    BucketName = _settings.BucketName,
                    Objects = keysToDelete
                };

                await _s3Client.DeleteObjectsAsync(deleteRequest, cancellationToken);
                _logger.LogInformation("Deleted {Count} files for ToolboxTalk {TalkId}",
                    keysToDelete.Count, toolboxTalkId);
            }
            else
            {
                _logger.LogInformation("No files found to delete for ToolboxTalk {TalkId}", toolboxTalkId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete files for ToolboxTalk {TalkId}", toolboxTalkId);
            throw;
        }
    }

    public async Task DeleteVideoAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        await DeleteFilesByFolderAsync(tenantId, toolboxTalkId, VideosFolder, cancellationToken);
    }

    public async Task DeletePdfAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        await DeleteFilesByFolderAsync(tenantId, toolboxTalkId, PdfsFolder, cancellationToken);
    }

    private async Task DeleteFilesByFolderAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string folder,
        CancellationToken cancellationToken)
    {
        try
        {
            var shortId = toolboxTalkId.ToString("N")[..8];
            var prefix = $"{tenantId}/{folder}/";

            _logger.LogInformation("Deleting {Folder} files for ToolboxTalk {TalkId} with prefix {Prefix}",
                folder, toolboxTalkId, prefix);

            var listRequest = new ListObjectsV2Request
            {
                BucketName = _settings.BucketName,
                Prefix = prefix
            };

            var response = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);

            var keysToDelete = response.S3Objects
                .Where(obj => obj.Key.Contains($"_{shortId}.") || obj.Key.Contains($"_{shortId}_"))
                .Select(obj => new KeyVersion { Key = obj.Key })
                .ToList();

            if (keysToDelete.Count > 0)
            {
                var deleteRequest = new DeleteObjectsRequest
                {
                    BucketName = _settings.BucketName,
                    Objects = keysToDelete
                };

                await _s3Client.DeleteObjectsAsync(deleteRequest, cancellationToken);
                _logger.LogInformation("Deleted {Count} {Folder} file(s) for ToolboxTalk {TalkId}",
                    keysToDelete.Count, folder, toolboxTalkId);
            }
            else
            {
                _logger.LogInformation("No {Folder} files found to delete for ToolboxTalk {TalkId}",
                    folder, toolboxTalkId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete {Folder} files for ToolboxTalk {TalkId}",
                folder, toolboxTalkId);
            throw;
        }
    }

    #endregion

    #region Utilities

    public string GeneratePublicUrl(Guid tenantId, string folder, string fileName)
    {
        var publicUrl = _settings.PublicUrl.TrimEnd('/');
        var key = BuildKey(tenantId, folder, fileName);
        var encodedKey = Uri.EscapeDataString(key).Replace("%2F", "/");
        return $"{publicUrl}/{encodedKey}";
    }

    private static string BuildKey(Guid tenantId, string folder, string fileName)
    {
        return $"{tenantId}/{folder}/{fileName}";
    }

    private static string GetVideoContentType(string extension)
    {
        return extension.ToLower() switch
        {
            "mp4" => "video/mp4",
            "webm" => "video/webm",
            "mov" => "video/quicktime",
            _ => "video/mp4"
        };
    }

    public void Dispose()
    {
        _s3Client.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
