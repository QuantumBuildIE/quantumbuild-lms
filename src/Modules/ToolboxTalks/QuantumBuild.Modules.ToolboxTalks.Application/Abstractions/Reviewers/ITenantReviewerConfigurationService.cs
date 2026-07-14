using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reviewers;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Reviewers;

public interface ITenantReviewerConfigurationService
{
    Task<List<TenantReviewerConfigurationDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantReviewerConfigurationDto> CreateAsync(
        Guid tenantId,
        string? languageCode,
        string reviewerEmail,
        string? reviewerName,
        CancellationToken cancellationToken = default);

    Task<TenantReviewerConfigurationDto> UpdateAsync(
        Guid tenantId,
        Guid id,
        string reviewerEmail,
        string? reviewerName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}
