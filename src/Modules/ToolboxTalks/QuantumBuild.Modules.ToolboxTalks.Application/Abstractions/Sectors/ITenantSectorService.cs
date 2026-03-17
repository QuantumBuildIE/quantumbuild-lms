using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Sectors;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Sectors;

public interface ITenantSectorService
{
    Task<List<TenantSectorDto>> GetTenantSectorsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantSectorDto> AssignSectorAsync(Guid tenantId, Guid sectorId, bool isDefault, CancellationToken cancellationToken = default);
    Task RemoveSectorAsync(Guid tenantId, Guid sectorId, CancellationToken cancellationToken = default);
    Task SetDefaultSectorAsync(Guid tenantId, Guid sectorId, CancellationToken cancellationToken = default);
    Task<TenantSectorDto?> GetDefaultSectorAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
