namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Request body for creating a new glossary sector
/// </summary>
public record CreateSectorRequest
{
    public string SectorKey { get; init; } = string.Empty;
    public string SectorName { get; init; } = string.Empty;
    public string? SectorIcon { get; init; }
}
