using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.Lookups.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.Lookups;

public class LookupService : ILookupService
{
    private readonly ICoreDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public LookupService(ICoreDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<LookupValueDto>>> GetEffectiveValuesAsync(Guid tenantId, string categoryName, bool includeDisabled = false)
    {
        try
        {
            var category = await _context.LookupCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == categoryName && c.IsActive);

            if (category == null)
                return Result.Fail<List<LookupValueDto>>($"Lookup category '{categoryName}' not found");

            // Get global default values for this category
            var globalValues = await _context.LookupValues
                .AsNoTracking()
                .Where(v => v.CategoryId == category.Id && v.IsActive)
                .ToListAsync();

            // Get tenant-specific values (bypasses tenant query filter via explicit tenantId match)
            var tenantValues = await _context.TenantLookupValues
                .AsNoTracking()
                .Where(v => v.CategoryId == category.Id && v.TenantId == tenantId)
                .ToListAsync();

            var result = new List<LookupValueDto>();

            // Add global values that haven't been overridden by tenant values
            var overriddenGlobalIds = tenantValues
                .Where(tv => tv.LookupValueId.HasValue)
                .Select(tv => tv.LookupValueId!.Value)
                .ToHashSet();

            foreach (var gv in globalValues)
            {
                if (overriddenGlobalIds.Contains(gv.Id))
                {
                    // Use the tenant override instead
                    var tenantOverride = tenantValues.First(tv => tv.LookupValueId == gv.Id);
                    if (tenantOverride.IsEnabled || includeDisabled)
                    {
                        result.Add(new LookupValueDto(
                            tenantOverride.Id,
                            category.Id,
                            tenantOverride.Code,
                            tenantOverride.Name,
                            tenantOverride.Metadata,
                            tenantOverride.SortOrder,
                            tenantOverride.IsEnabled,
                            IsGlobal: false,
                            LookupValueId: gv.Id
                        ));
                    }
                    // If !IsEnabled and !includeDisabled, the global value is suppressed for this tenant
                }
                else
                {
                    result.Add(new LookupValueDto(
                        gv.Id,
                        category.Id,
                        gv.Code,
                        gv.Name,
                        gv.Metadata,
                        gv.SortOrder,
                        gv.IsActive,
                        IsGlobal: true,
                        LookupValueId: null
                    ));
                }
            }

            // Add tenant custom values only when category allows custom values
            if (category.AllowCustom)
            {
                foreach (var tv in tenantValues.Where(tv => !tv.LookupValueId.HasValue && tv.IsEnabled && !tv.IsDeleted))
                {
                    result.Add(new LookupValueDto(
                        tv.Id,
                        category.Id,
                        tv.Code,
                        tv.Name,
                        tv.Metadata,
                        tv.SortOrder,
                        true,
                        IsGlobal: false,
                        LookupValueId: null
                    ));
                }
            }

            return Result.Ok(result.OrderBy(v => v.Name).ThenBy(v => v.SortOrder).ToList());
        }
        catch (Exception ex)
        {
            return Result.Fail<List<LookupValueDto>>($"Error retrieving lookup values: {ex.Message}");
        }
    }

    public async Task<Result<List<LookupCategoryDto>>> GetCategoriesAsync()
    {
        try
        {
            var categories = await _context.LookupCategories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new LookupCategoryDto(
                    c.Id,
                    c.Name,
                    c.Module,
                    c.AllowCustom,
                    c.IsActive
                ))
                .ToListAsync();

            return Result.Ok(categories);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<LookupCategoryDto>>($"Error retrieving lookup categories: {ex.Message}");
        }
    }

    public async Task<Result<LookupValueDto>> CreateTenantValueAsync(string categoryName, CreateTenantLookupValueDto dto)
    {
        try
        {
            var category = await _context.LookupCategories
                .FirstOrDefaultAsync(c => c.Name == categoryName && c.IsActive);

            if (category == null)
                return Result.Fail<LookupValueDto>($"Lookup category '{categoryName}' not found");

            if (!category.AllowCustom)
                return Result.Fail<LookupValueDto>($"Category '{categoryName}' does not allow custom values");

            var tenantId = _currentUserService.TenantId;

            // Check for duplicate code within this tenant and category
            var duplicate = await _context.TenantLookupValues
                .AnyAsync(v => v.TenantId == tenantId && v.CategoryId == category.Id && v.Code == dto.Code);

            if (duplicate)
                return Result.Fail<LookupValueDto>($"A value with code '{dto.Code}' already exists in this category");

            var tenantValue = new TenantLookupValue
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Code = dto.Code,
                Name = dto.Name,
                Metadata = dto.Metadata,
                SortOrder = dto.SortOrder,
                IsEnabled = true
            };

            _context.TenantLookupValues.Add(tenantValue);
            await _context.SaveChangesAsync();

            return Result.Ok(new LookupValueDto(
                tenantValue.Id,
                category.Id,
                tenantValue.Code,
                tenantValue.Name,
                tenantValue.Metadata,
                tenantValue.SortOrder,
                true,
                IsGlobal: false,
                LookupValueId: null
            ));
        }
        catch (Exception ex)
        {
            return Result.Fail<LookupValueDto>($"Error creating lookup value: {ex.Message}");
        }
    }

    public async Task<Result<LookupValueDto>> UpdateTenantValueAsync(Guid id, UpdateTenantLookupValueDto dto)
    {
        try
        {
            var tenantValue = await _context.TenantLookupValues
                .FirstOrDefaultAsync(v => v.Id == id);

            if (tenantValue == null)
                return Result.Fail<LookupValueDto>($"Tenant lookup value with ID {id} not found");

            // Check for duplicate code (excluding current value)
            var duplicate = await _context.TenantLookupValues
                .AnyAsync(v => v.TenantId == tenantValue.TenantId
                    && v.CategoryId == tenantValue.CategoryId
                    && v.Code == dto.Code
                    && v.Id != id);

            if (duplicate)
                return Result.Fail<LookupValueDto>($"A value with code '{dto.Code}' already exists in this category");

            tenantValue.Code = dto.Code;
            tenantValue.Name = dto.Name;
            tenantValue.Metadata = dto.Metadata;
            tenantValue.SortOrder = dto.SortOrder;
            tenantValue.IsEnabled = dto.IsEnabled;

            await _context.SaveChangesAsync();

            return Result.Ok(new LookupValueDto(
                tenantValue.Id,
                tenantValue.CategoryId,
                tenantValue.Code,
                tenantValue.Name,
                tenantValue.Metadata,
                tenantValue.SortOrder,
                tenantValue.IsEnabled,
                IsGlobal: false,
                LookupValueId: tenantValue.LookupValueId
            ));
        }
        catch (Exception ex)
        {
            return Result.Fail<LookupValueDto>($"Error updating lookup value: {ex.Message}");
        }
    }

    public async Task<Result> DeleteTenantValueAsync(Guid id)
    {
        try
        {
            var tenantValue = await _context.TenantLookupValues
                .FirstOrDefaultAsync(v => v.Id == id);

            if (tenantValue == null)
                return Result.Fail($"Tenant lookup value with ID {id} not found");

            // Don't allow deleting overrides of global values — disable instead
            if (tenantValue.LookupValueId.HasValue)
                return Result.Fail("Cannot delete an override of a global value. Disable it instead.");

            tenantValue.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Error deleting lookup value: {ex.Message}");
        }
    }

    public async Task<Result<LookupValueDto>> ToggleGlobalValueAsync(string categoryName, Guid lookupValueId, bool isEnabled)
    {
        try
        {
            var category = await _context.LookupCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Name == categoryName && c.IsActive);

            if (category == null)
                return Result.Fail<LookupValueDto>($"Lookup category '{categoryName}' not found");

            // Verify the global value exists
            var globalValue = await _context.LookupValues
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == lookupValueId && v.CategoryId == category.Id && v.IsActive);

            if (globalValue == null)
                return Result.Fail<LookupValueDto>($"Global lookup value with ID {lookupValueId} not found in category '{categoryName}'");

            var tenantId = _currentUserService.TenantId;

            // Check if a tenant override already exists
            var tenantOverride = await _context.TenantLookupValues
                .FirstOrDefaultAsync(v => v.TenantId == tenantId
                    && v.CategoryId == category.Id
                    && v.LookupValueId == lookupValueId);

            if (tenantOverride != null)
            {
                tenantOverride.IsEnabled = isEnabled;
                await _context.SaveChangesAsync();

                return Result.Ok(new LookupValueDto(
                    tenantOverride.Id,
                    category.Id,
                    tenantOverride.Code,
                    tenantOverride.Name,
                    tenantOverride.Metadata,
                    tenantOverride.SortOrder,
                    tenantOverride.IsEnabled,
                    IsGlobal: false,
                    LookupValueId: globalValue.Id
                ));
            }

            // Create a new tenant override
            var newOverride = new TenantLookupValue
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                LookupValueId = globalValue.Id,
                Code = globalValue.Code,
                Name = globalValue.Name,
                Metadata = globalValue.Metadata,
                SortOrder = globalValue.SortOrder,
                IsEnabled = isEnabled
            };

            _context.TenantLookupValues.Add(newOverride);
            await _context.SaveChangesAsync();

            return Result.Ok(new LookupValueDto(
                newOverride.Id,
                category.Id,
                newOverride.Code,
                newOverride.Name,
                newOverride.Metadata,
                newOverride.SortOrder,
                newOverride.IsEnabled,
                IsGlobal: false,
                LookupValueId: globalValue.Id
            ));
        }
        catch (Exception ex)
        {
            return Result.Fail<LookupValueDto>($"Error toggling lookup value: {ex.Message}");
        }
    }
}
