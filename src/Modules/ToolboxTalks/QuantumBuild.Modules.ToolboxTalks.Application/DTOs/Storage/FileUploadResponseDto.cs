namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Storage;

/// <summary>
/// Response DTO for file upload operations
/// </summary>
public record FileUploadResponseDto
{
    /// <summary>
    /// Whether the upload was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Public URL to access the uploaded file
    /// </summary>
    public string? PublicUrl { get; init; }

    /// <summary>
    /// Name of the uploaded file
    /// </summary>
    public string? FileName { get; init; }

    /// <summary>
    /// Size of the uploaded file in bytes
    /// </summary>
    public long? FileSizeBytes { get; init; }

    /// <summary>
    /// Content type of the uploaded file
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// Error message if upload failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static FileUploadResponseDto FromSuccess(string publicUrl, string fileName, long fileSize, string contentType) =>
        new()
        {
            Success = true,
            PublicUrl = publicUrl,
            FileName = fileName,
            FileSizeBytes = fileSize,
            ContentType = contentType
        };

    public static FileUploadResponseDto FromError(string errorMessage) =>
        new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
}
