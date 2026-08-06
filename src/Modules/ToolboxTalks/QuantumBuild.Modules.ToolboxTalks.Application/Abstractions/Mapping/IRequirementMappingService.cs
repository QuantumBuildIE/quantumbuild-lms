using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Frameworks;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Mapping;

/// <summary>
/// Service for managing AI-suggested regulatory requirement mappings.
/// Tenant-scoped — all operations use ICurrentUserService.TenantId.
/// </summary>
public interface IRequirementMappingService
{
    Task<MappingSummaryDto> GetPendingMappingsAsync(CancellationToken cancellationToken = default);
    Task<PendingMappingDto> ConfirmMappingAsync(Guid mappingId, CancellationToken cancellationToken = default);
    Task RejectMappingAsync(Guid mappingId, string? notes, CancellationToken cancellationToken = default);
    Task<int> ConfirmAllSuggestedAsync(CancellationToken cancellationToken = default);
    Task<int> GetUnconfirmedCountAsync(Guid? toolboxTalkId, Guid? courseId, CancellationToken cancellationToken = default);
    Task<ComplianceChecklistDto> GetComplianceChecklistAsync(string sectorKey, CancellationToken cancellationToken = default);
    Task<PendingMappingDto> AddManualMappingAsync(AddManualMappingRequest request, CancellationToken cancellationToken = default);
    Task<List<ContentOptionDto>> GetContentOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Regulatory frameworks (Regulations via sector + Standards via subscription) currently
    /// applicable to the current tenant, with approved requirement counts — powers the
    /// compliance page's top-level summary.
    /// </summary>
    Task<List<ApplicableFrameworkDto>> GetApplicableFrameworksAsync(CancellationToken cancellationToken = default);
}
