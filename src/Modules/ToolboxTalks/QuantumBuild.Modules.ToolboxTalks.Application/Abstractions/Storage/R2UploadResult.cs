namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;

/// <summary>
/// Result of an R2 upload operation
/// </summary>
public class R2UploadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? PublicUrl { get; set; }
    public string? Key { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? ContentType { get; set; }

    public static R2UploadResult SuccessResult(string publicUrl, string key, long fileSize, string contentType) =>
        new()
        {
            Success = true,
            PublicUrl = publicUrl,
            Key = key,
            FileSizeBytes = fileSize,
            ContentType = contentType
        };

    public static R2UploadResult FailureResult(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
