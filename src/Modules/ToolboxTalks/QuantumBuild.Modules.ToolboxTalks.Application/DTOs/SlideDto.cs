namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

/// <summary>
/// DTO for a toolbox talk slide with optional translated text
/// </summary>
public record SlideDto
{
    public Guid Id { get; init; }
    public int PageNumber { get; init; }
    public string ImageUrl { get; init; } = string.Empty;
    public string? Text { get; init; }
}
