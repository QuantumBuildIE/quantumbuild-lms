namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Sectors;

public record TenantSectorDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid SectorId { get; init; }
    public string SectorKey { get; init; } = string.Empty;
    public string SectorName { get; init; } = string.Empty;
    public string? SectorIcon { get; init; }
    public bool IsDefault { get; init; }
}
