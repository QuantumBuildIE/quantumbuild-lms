using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Standards;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Standards;

public interface ITenantStandardSubscriptionService
{
    Task<List<AvailableStandardDto>> GetAvailableStandardsAsync(
        Guid tenantId, bool includeCrossSector, CancellationToken cancellationToken = default);

    Task<List<TenantStandardSubscriptionDto>> GetSubscribedStandardsAsync(
        Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantStandardSubscriptionDto> SubscribeAsync(
        Guid tenantId, Guid regulatoryBodyId, CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(
        Guid tenantId, Guid regulatoryBodyId, CancellationToken cancellationToken = default);
}
