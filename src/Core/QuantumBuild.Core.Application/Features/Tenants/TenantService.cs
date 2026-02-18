using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.Tenants.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Domain.Enums;

namespace QuantumBuild.Core.Application.Features.Tenants;

public class TenantService : ITenantService
{
    private readonly ICoreDbContext _context;

    public TenantService(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<TenantListDto>>> GetPaginatedAsync(
        int pageNumber = 1,
        int pageSize = 20,
        string? sortColumn = null,
        string? sortDirection = null,
        string? search = null)
    {
        try
        {
            var query = _context.Tenants
                .IgnoreQueryFilters()
                .Where(t => !t.IsDeleted)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(t =>
                    t.Name.ToLower().Contains(searchLower) ||
                    (t.Code != null && t.Code.ToLower().Contains(searchLower)) ||
                    (t.CompanyName != null && t.CompanyName.ToLower().Contains(searchLower)) ||
                    (t.ContactEmail != null && t.ContactEmail.ToLower().Contains(searchLower)) ||
                    (t.ContactName != null && t.ContactName.ToLower().Contains(searchLower)));
            }

            query = ApplySorting(query, sortColumn, sortDirection);

            var totalCount = await query.CountAsync();

            var tenants = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TenantListDto(
                    t.Id,
                    t.Name,
                    t.Code,
                    t.CompanyName,
                    t.Status,
                    t.ContactEmail,
                    t.ContactName,
                    t.IsActive,
                    t.CreatedAt))
                .ToListAsync();

            var paginatedList = new PaginatedList<TenantListDto>(tenants, totalCount, pageNumber, pageSize);
            return Result.Ok(paginatedList);
        }
        catch (Exception ex)
        {
            return Result.Fail<PaginatedList<TenantListDto>>($"Error retrieving tenants: {ex.Message}");
        }
    }

    public async Task<Result<TenantDetailDto>> GetByIdAsync(Guid id)
    {
        try
        {
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (tenant == null)
                return Result.Fail<TenantDetailDto>("Tenant not found");

            return Result.Ok(MapToDetailDto(tenant));
        }
        catch (Exception ex)
        {
            return Result.Fail<TenantDetailDto>($"Error retrieving tenant: {ex.Message}");
        }
    }

    public async Task<Result<TenantDetailDto>> CreateAsync(CreateTenantCommand command)
    {
        try
        {
            var existingByName = await _context.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Name.ToLower() == command.Name.ToLower() && !t.IsDeleted);

            if (existingByName)
                return Result.Fail<TenantDetailDto>("A tenant with this name already exists");

            if (!string.IsNullOrWhiteSpace(command.Code))
            {
                var existingByCode = await _context.Tenants
                    .IgnoreQueryFilters()
                    .AnyAsync(t => t.Code != null && t.Code.ToLower() == command.Code.ToLower() && !t.IsDeleted);

                if (existingByCode)
                    return Result.Fail<TenantDetailDto>("A tenant with this code already exists");
            }

            var tenant = new Tenant
            {
                Name = command.Name,
                Code = command.Code,
                CompanyName = command.CompanyName,
                ContactEmail = command.ContactEmail,
                ContactName = command.ContactName,
                Status = TenantStatus.Active,
                IsActive = true
            };

            _context.Tenants.Add(tenant);
            await _context.SaveChangesAsync();

            return Result.Ok(MapToDetailDto(tenant));
        }
        catch (Exception ex)
        {
            return Result.Fail<TenantDetailDto>($"Error creating tenant: {ex.Message}");
        }
    }

    public async Task<Result<TenantDetailDto>> UpdateAsync(Guid id, UpdateTenantCommand command)
    {
        try
        {
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (tenant == null)
                return Result.Fail<TenantDetailDto>("Tenant not found");

            var existingByName = await _context.Tenants
                .IgnoreQueryFilters()
                .AnyAsync(t => t.Id != id && t.Name.ToLower() == command.Name.ToLower() && !t.IsDeleted);

            if (existingByName)
                return Result.Fail<TenantDetailDto>("A tenant with this name already exists");

            if (!string.IsNullOrWhiteSpace(command.Code))
            {
                var existingByCode = await _context.Tenants
                    .IgnoreQueryFilters()
                    .AnyAsync(t => t.Id != id && t.Code != null && t.Code.ToLower() == command.Code.ToLower() && !t.IsDeleted);

                if (existingByCode)
                    return Result.Fail<TenantDetailDto>("A tenant with this code already exists");
            }

            tenant.Name = command.Name;
            tenant.Code = command.Code;
            tenant.CompanyName = command.CompanyName;
            tenant.ContactEmail = command.ContactEmail;
            tenant.ContactName = command.ContactName;

            await _context.SaveChangesAsync();

            return Result.Ok(MapToDetailDto(tenant));
        }
        catch (Exception ex)
        {
            return Result.Fail<TenantDetailDto>($"Error updating tenant: {ex.Message}");
        }
    }

    public async Task<Result<TenantDetailDto>> UpdateStatusAsync(Guid id, UpdateTenantStatusCommand command)
    {
        try
        {
            var tenant = await _context.Tenants
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

            if (tenant == null)
                return Result.Fail<TenantDetailDto>("Tenant not found");

            tenant.Status = command.Status;
            tenant.IsActive = command.Status == TenantStatus.Active;

            await _context.SaveChangesAsync();

            return Result.Ok(MapToDetailDto(tenant));
        }
        catch (Exception ex)
        {
            return Result.Fail<TenantDetailDto>($"Error updating tenant status: {ex.Message}");
        }
    }

    private static TenantDetailDto MapToDetailDto(Tenant tenant) =>
        new(
            tenant.Id,
            tenant.Name,
            tenant.Code,
            tenant.CompanyName,
            tenant.Status,
            tenant.ContactEmail,
            tenant.ContactName,
            tenant.IsActive,
            tenant.CreatedAt,
            tenant.CreatedBy,
            tenant.UpdatedAt,
            tenant.UpdatedBy);

    private static IQueryable<Tenant> ApplySorting(IQueryable<Tenant> query, string? sortColumn, string? sortDirection)
    {
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortColumn?.ToLower() switch
        {
            "name" => isDescending ? query.OrderByDescending(t => t.Name) : query.OrderBy(t => t.Name),
            "code" => isDescending ? query.OrderByDescending(t => t.Code) : query.OrderBy(t => t.Code),
            "companyname" => isDescending ? query.OrderByDescending(t => t.CompanyName) : query.OrderBy(t => t.CompanyName),
            "status" => isDescending ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
            "contactemail" => isDescending ? query.OrderByDescending(t => t.ContactEmail) : query.OrderBy(t => t.ContactEmail),
            "createdat" => isDescending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt),
            _ => query.OrderBy(t => t.Name)
        };
    }
}
