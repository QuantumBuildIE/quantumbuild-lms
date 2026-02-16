using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;

/// <summary>
/// SRT storage provider using Cloudflare R2 (S3-compatible).
/// Uploads SRT files to a Cloudflare R2 bucket for public access.
/// </summary>
public class CloudflareR2SrtStorageProvider : ISrtStorageProvider, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<CloudflareR2SrtStorageProvider> _logger;

    public CloudflareR2SrtStorageProvider(
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<CloudflareR2SrtStorageProvider> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var r2Settings = _settings.SrtStorage.CloudflareR2;

        _logger.LogInformation(
            "[R2 SRT Provider] Initializing with ServiceUrl: {ServiceUrl}, Bucket: {Bucket}, HasAccessKey: {HasKey}, HasSecretKey: {HasSecret}",
            string.IsNullOrEmpty(r2Settings.ServiceUrl) ? "NULL" : r2Settings.ServiceUrl,
            r2Settings.BucketName ?? "NULL",
            !string.IsNullOrEmpty(r2Settings.AccessKeyId),
            !string.IsNullOrEmpty(r2Settings.SecretAccessKey));

        var config = new AmazonS3Config
        {
            ServiceURL = r2Settings.ServiceUrl,
            ForcePathStyle = true
        };

        _s3Client = new AmazonS3Client(
            r2Settings.AccessKeyId,
            r2Settings.SecretAccessKey,
            config);
    }

    /// <summary>
    /// Uploads an SRT file to the configured Cloudflare R2 bucket.
    /// Files are stored under {tenantId}/{path}/{fileName} for tenant isolation.
    /// </summary>
    public async Task<SrtUploadResult> UploadSrtAsync(
        string srtContent,
        string fileName,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var r2Settings = _settings.SrtStorage.CloudflareR2;

        _logger.LogInformation(
            "[R2 SRT Upload] Config check - ServiceUrl: {ServiceUrl}, Bucket: {Bucket}, HasAccessKey: {HasKey}, HasSecretKey: {HasSecret}, Path: {Path}, TenantId: {TenantId}",
            string.IsNullOrEmpty(r2Settings.ServiceUrl) ? "NULL" : r2Settings.ServiceUrl,
            r2Settings.BucketName ?? "NULL",
            !string.IsNullOrEmpty(r2Settings.AccessKeyId),
            !string.IsNullOrEmpty(r2Settings.SecretAccessKey),
            r2Settings.Path ?? "NULL",
            tenantId);

        _logger.LogInformation(
            "[R2 SRT Upload] Starting upload for FileName: {FileName}, TenantId: {TenantId}",
            fileName, tenantId);

        try
        {

            // Ensure .srt extension
            if (!fileName.EndsWith(".srt", StringComparison.OrdinalIgnoreCase))
                fileName += ".srt";

            // Build the key (path within bucket) with tenant isolation: {tenantId}/{path}/{fileName}
            var key = string.IsNullOrEmpty(r2Settings.Path)
                ? $"{tenantId}/{fileName}"
                : $"{tenantId}/{r2Settings.Path.TrimEnd('/')}/{fileName}";

            _logger.LogInformation("Uploading SRT to Cloudflare R2: {Key}", key);

            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(srtContent));

            var request = new PutObjectRequest
            {
                BucketName = r2Settings.BucketName,
                Key = key,
                InputStream = stream,
                ContentType = "application/x-subrip",

                // Disable features not supported by Cloudflare R2
                // R2 doesn't support STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER
                DisablePayloadSigning = true,
                UseChunkEncoding = false
            };

            await _s3Client.PutObjectAsync(request, cancellationToken);

            // Build public URL
            var publicUrl = GetPublicUrl(key);

            _logger.LogInformation("Successfully uploaded SRT to Cloudflare R2: {Url}", publicUrl);

            return SrtUploadResult.SuccessResult(publicUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[R2 SRT Upload] Exception during upload. FileName: {FileName}, Bucket: {Bucket}, ServiceUrl: {ServiceUrl}",
                fileName, r2Settings.BucketName, r2Settings.ServiceUrl);
            return SrtUploadResult.FailureResult($"Upload failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves SRT content from the Cloudflare R2 bucket.
    /// </summary>
    public async Task<string?> GetSrtContentAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var r2Settings = _settings.SrtStorage.CloudflareR2;

            var key = string.IsNullOrEmpty(r2Settings.Path)
                ? fileName
                : $"{r2Settings.Path.TrimEnd('/')}/{fileName}";

            var request = new GetObjectRequest
            {
                BucketName = r2Settings.BucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request, cancellationToken);
            using var reader = new StreamReader(response.ResponseStream);

            return await reader.ReadToEndAsync(cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("SRT file not found in R2: {FileName}", fileName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get SRT from Cloudflare R2: {FileName}", fileName);
            return null;
        }
    }

    /// <summary>
    /// Deletes an SRT file from the Cloudflare R2 bucket.
    /// </summary>
    public async Task<bool> DeleteSrtAsync(string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var r2Settings = _settings.SrtStorage.CloudflareR2;

            var key = string.IsNullOrEmpty(r2Settings.Path)
                ? fileName
                : $"{r2Settings.Path.TrimEnd('/')}/{fileName}";

            var request = new DeleteObjectRequest
            {
                BucketName = r2Settings.BucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request, cancellationToken);

            _logger.LogInformation("Successfully deleted SRT from Cloudflare R2: {FileName}", fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete SRT from Cloudflare R2: {FileName}", fileName);
            return false;
        }
    }

    /// <summary>
    /// Gets the public URL for a file in the R2 bucket.
    /// </summary>
    private string GetPublicUrl(string key)
    {
        var publicUrl = _settings.SrtStorage.CloudflareR2.PublicUrl.TrimEnd('/');
        var encodedKey = Uri.EscapeDataString(key).Replace("%2F", "/");
        return $"{publicUrl}/{encodedKey}";
    }

    public void Dispose()
    {
        _s3Client.Dispose();
    }
}
