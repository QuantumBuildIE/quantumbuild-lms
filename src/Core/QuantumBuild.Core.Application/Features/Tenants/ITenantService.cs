using QuantumBuild.Core.Application.Features.Tenants.DTOs;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.Tenants;

public interface ITenantService
{
    Task<Result<PaginatedList<TenantListDto>>> GetPaginatedAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? sortColumn = null,
        string? sortDirection = null,
        string? search = null);
    Task<Result<TenantDetailDto>> GetByIdAsync(Guid id);
    Task<Result<TenantDetailDto>> CreateAsync(CreateTenantCommand command);
    Task<Result<TenantDetailDto>> UpdateAsync(Guid id, UpdateTenantCommand command);
    Task<Result<TenantDetailDto>> UpdateStatusAsync(Guid id, UpdateTenantStatusCommand command);
}
