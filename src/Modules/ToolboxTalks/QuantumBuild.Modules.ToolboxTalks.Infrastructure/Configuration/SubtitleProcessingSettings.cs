namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for subtitle processing functionality.
/// Binds to the "SubtitleProcessing" section in appsettings.json.
/// </summary>
public class SubtitleProcessingSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "SubtitleProcessing";

    /// <summary>
    /// Number of subtitles to translate in each batch.
    /// Default: 30
    /// </summary>
    public int BatchSize { get; set; } = 30;

    /// <summary>
    /// Target number of words per subtitle entry.
    /// Used when generating SRT from transcription.
    /// Default: 8
    /// </summary>
    public int WordsPerSubtitle { get; set; } = 8;

    /// <summary>
    /// Maximum allowed video size in megabytes.
    /// Default: 500 MB
    /// </summary>
    public int MaxVideoSizeMb { get; set; } = 500;

    /// <summary>
    /// ElevenLabs API settings for transcription
    /// </summary>
    public ElevenLabsSettings ElevenLabs { get; set; } = new();

    /// <summary>
    /// Claude API settings for translation
    /// </summary>
    public ClaudeSettings Claude { get; set; } = new();

    /// <summary>
    /// SRT file storage settings
    /// </summary>
    public SrtStorageSettings SrtStorage { get; set; } = new();

    /// <summary>
    /// Video source settings (where videos come from)
    /// </summary>
    public VideoSourceSettings VideoSource { get; set; } = new();
}

/// <summary>
/// ElevenLabs API configuration for audio transcription
/// </summary>
public class ElevenLabsSettings
{
    /// <summary>
    /// ElevenLabs API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Transcription model to use.
    /// Default: scribe_v1
    /// </summary>
    public string Model { get; set; } = "scribe_v1";

    /// <summary>
    /// ElevenLabs API base URL.
    /// Default: https://api.elevenlabs.io/v1
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.elevenlabs.io/v1";
}

/// <summary>
/// Claude API configuration for subtitle translation
/// </summary>
public class ClaudeSettings
{
    /// <summary>
    /// Anthropic API key for Claude
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Claude model to use for translation.
    /// Default: claude-sonnet-4-20250514
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>
    /// Maximum tokens for translation responses.
    /// Default: 4000
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Anthropic API base URL.
    /// Default: https://api.anthropic.com/v1
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1";
}

/// <summary>
/// Storage settings for generated SRT files
/// </summary>
public class SrtStorageSettings
{
    /// <summary>
    /// Storage provider type: "GitHub", "AzureBlob", "CloudflareR2", or "Database"
    /// Default: CloudflareR2
    /// </summary>
    public string Type { get; set; } = "CloudflareR2";

    /// <summary>
    /// GitHub storage settings
    /// </summary>
    public GitHubStorageSettings GitHub { get; set; } = new();

    /// <summary>
    /// Azure Blob storage settings
    /// </summary>
    public AzureBlobStorageSettings AzureBlob { get; set; } = new();

    /// <summary>
    /// Cloudflare R2 storage settings (S3-compatible)
    /// </summary>
    public CloudflareR2Settings CloudflareR2 { get; set; } = new();
}

/// <summary>
/// GitHub repository settings for SRT file storage
/// </summary>
public class GitHubStorageSettings
{
    /// <summary>
    /// GitHub Personal Access Token with repo write access
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// GitHub repository owner (username or organization)
    /// Default: DOSCA61
    /// </summary>
    public string Owner { get; set; } = "DOSCA61";

    /// <summary>
    /// GitHub repository name
    /// Default: toolbox-video
    /// </summary>
    public string Repo { get; set; } = "toolbox-video";

    /// <summary>
    /// Branch to commit files to
    /// Default: main
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Path within the repository for SRT files
    /// Default: subs
    /// </summary>
    public string Path { get; set; } = "subs";
}

/// <summary>
/// Azure Blob Storage settings for SRT file storage
/// </summary>
public class AzureBlobStorageSettings
{
    /// <summary>
    /// Azure Storage connection string
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Blob container name for subtitles
    /// Default: subtitles
    /// </summary>
    public string Container { get; set; } = "subtitles";
}

/// <summary>
/// Cloudflare R2 storage settings (S3-compatible API)
/// </summary>
public class CloudflareR2Settings
{
    /// <summary>
    /// Cloudflare account ID (optional, kept for reference/logging)
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// The S3-compatible service URL for Cloudflare R2.
    /// Different jurisdictions have different endpoints:
    /// - Default: https://{accountId}.r2.cloudflarestorage.com
    /// - EU: https://{accountId}.eu.r2.cloudflarestorage.com
    /// </summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>
    /// R2 API access key ID
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;

    /// <summary>
    /// R2 API secret access key
    /// </summary>
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// R2 bucket name
    /// Default: rascor-videos
    /// </summary>
    public string BucketName { get; set; } = "rascor-videos";

    /// <summary>
    /// Public URL base for the R2 bucket (e.g., https://pub-xxx.r2.dev)
    /// </summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>
    /// Path within the bucket for SRT files
    /// Default: subs
    /// </summary>
    public string Path { get; set; } = "subs";
}

/// <summary>
/// Video source configuration for input videos
/// </summary>
public class VideoSourceSettings
{
    /// <summary>
    /// Supported video source types: "GoogleDrive", "AzureBlob", or "Both"
    /// Default: Both
    /// </summary>
    public string Type { get; set; } = "Both";

    /// <summary>
    /// Azure Blob connection string for video storage
    /// </summary>
    public string AzureBlobConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Azure Blob container for videos
    /// Default: toolbox-videos
    /// </summary>
    public string AzureBlobContainer { get; set; } = "toolbox-videos";
}
