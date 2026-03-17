using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Sectors;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Sectors;

public interface ISectorService
{
    Task<List<SectorDto>> GetActiveSectorsAsync(CancellationToken cancellationToken = default);
}
