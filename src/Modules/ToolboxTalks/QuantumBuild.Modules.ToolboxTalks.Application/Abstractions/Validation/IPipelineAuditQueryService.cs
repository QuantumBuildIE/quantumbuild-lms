using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

public interface IPipelineAuditQueryService
{
    Task<PaginatedList<ModuleOutcomeDto>> GetModuleOutcomesAsync(
        Guid? tenantId,
        ValidationOutcome? outcome,
        string? languageCode,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<PipelineAuditDashboardDto> GetDashboardSummaryAsync(Guid? tenantId, CancellationToken ct = default);

    Task<PipelineVersion?> GetActivePipelineVersionAsync(CancellationToken ct = default);

    Task<PaginatedList<PipelineChangeRecordDto>> GetChangeRecordsAsync(int page, int pageSize, CancellationToken ct = default);
}
