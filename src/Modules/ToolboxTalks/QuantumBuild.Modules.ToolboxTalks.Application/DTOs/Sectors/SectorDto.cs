namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Sectors;

public record SectorDto
{
    public Guid Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public int DisplayOrder { get; init; }
}
