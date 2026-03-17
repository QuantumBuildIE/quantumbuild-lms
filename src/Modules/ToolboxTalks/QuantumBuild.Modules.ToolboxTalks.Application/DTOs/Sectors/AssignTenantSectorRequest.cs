namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Sectors;

public record AssignTenantSectorRequest
{
    public Guid SectorId { get; init; }
    public bool IsDefault { get; init; }
}
