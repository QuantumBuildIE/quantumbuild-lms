namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Storage;

/// <summary>
/// Response DTO for video upload operations
/// </summary>
public record VideoUploadResponseDto(
    string VideoUrl,
    string FileName,
    long FileSize,
    string Source);
