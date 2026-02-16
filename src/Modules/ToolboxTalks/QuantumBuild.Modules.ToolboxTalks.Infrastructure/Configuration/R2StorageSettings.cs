namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for Cloudflare R2 storage with tenant isolation.
/// Binds to the "R2Storage" section in appsettings.json.
/// </summary>
public class R2StorageSettings
{
    public const string SectionName = "R2Storage";

    /// <summary>
    /// R2 bucket name (e.g., "rascor-media")
    /// </summary>
    public string BucketName { get; set; } = "rascor-media";

    /// <summary>
    /// Public URL base for the R2 bucket (e.g., "https://pub-xxxxx.r2.dev")
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>
    /// S3-compatible endpoint URL (e.g., "https://xxxxx.eu.r2.cloudflarestorage.com")
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// R2 API access key ID
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// R2 API secret access key
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Maximum video file size in bytes (default: 500MB)
    /// </summary>
    public long MaxVideoSizeBytes { get; set; } = 500 * 1024 * 1024;

    /// <summary>
    /// Maximum PDF file size in bytes (default: 50MB)
    /// </summary>
    public long MaxPdfSizeBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>
    /// Allowed video content types
    /// </summary>
    public string[] AllowedVideoTypes { get; set; } =
    [
        "video/mp4",
        "video/webm",
        "video/quicktime"
    ];

    /// <summary>
    /// Allowed PDF content types
    /// </summary>
    public string[] AllowedPdfTypes { get; set; } =
    [
        "application/pdf"
    ];
}
