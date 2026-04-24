using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

public interface ITranslationDeviationService
{
    Task<TranslationDeviationDto> CreateAsync(CreateDeviationRequest request, CancellationToken ct = default);
    Task<TranslationDeviationDto> UpdateStatusAsync(Guid id, DeviationStatus status, string? closedBy, CancellationToken ct = default);
    Task<PaginatedList<TranslationDeviationDto>> GetPagedAsync(DeviationStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<TranslationDeviationDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DeviationSummaryDto> GetSummaryAsync(CancellationToken ct = default);
}
