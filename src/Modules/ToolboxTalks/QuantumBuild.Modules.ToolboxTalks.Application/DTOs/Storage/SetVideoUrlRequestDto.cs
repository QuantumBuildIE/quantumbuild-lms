namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Storage;

/// <summary>
/// Request DTO for setting an external video URL
/// </summary>
public record SetVideoUrlRequestDto(string Url);

/// <summary>
/// Response DTO for setting an external video URL
/// </summary>
public record SetVideoUrlResponseDto(
    string VideoUrl,
    string Source);
