namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Storage;

/// <summary>
/// Response DTO for PDF upload operations
/// </summary>
public record PdfUploadResponseDto(
    string PdfUrl,
    string FileName,
    long FileSize);
