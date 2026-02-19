using QuantumBuild.Core.Application.Features.Lookups.DTOs;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.Lookups;

public interface ILookupService
{
    Task<Result<List<LookupValueDto>>> GetEffectiveValuesAsync(Guid tenantId, string categoryName, bool includeDisabled = false);
    Task<Result<List<LookupCategoryDto>>> GetCategoriesAsync();
    Task<Result<LookupValueDto>> CreateTenantValueAsync(string categoryName, CreateTenantLookupValueDto dto);
    Task<Result<LookupValueDto>> UpdateTenantValueAsync(Guid id, UpdateTenantLookupValueDto dto);
    Task<Result> DeleteTenantValueAsync(Guid id);
    Task<Result<LookupValueDto>> ToggleGlobalValueAsync(string categoryName, Guid lookupValueId, bool isEnabled);
}
