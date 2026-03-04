namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Request body for updating a glossary sector
/// </summary>
public record UpdateSectorRequest
{
    public string SectorName { get; init; } = string.Empty;
    public string? SectorIcon { get; init; }
}
