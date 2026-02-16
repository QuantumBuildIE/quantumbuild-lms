using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.Sites.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.Sites;

public class SiteService : ISiteService
{
    private readonly ICoreDbContext _context;

    public SiteService(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SiteDto>>> GetAllAsync()
    {
        try
        {
            var sites = await _context.Sites
                .Include(s => s.SiteManager)
                .Include(s => s.Company)
                .OrderBy(s => s.SiteCode)
                .Select(s => new SiteDto(
                    s.Id,
                    s.SiteCode,
                    s.SiteName,
                    s.Address,
                    s.City,
                    s.PostalCode,
                    s.SiteManagerId,
                    s.SiteManager != null ? s.SiteManager.FirstName + " " + s.SiteManager.LastName : null,
                    s.CompanyId,
                    s.Company != null ? s.Company.CompanyName : null,
                    s.Phone,
                    s.Email,
                    s.IsActive,
                    s.Notes,
                    s.Latitude,
                    s.Longitude,
                    s.GeofenceRadiusMeters,
                    s.FloatProjectId,
                    s.FloatLinkedAt,
                    s.FloatLinkMethod
                ))
                .ToListAsync();

            return Result.Ok(sites);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<SiteDto>>($"Error retrieving sites: {ex.Message}");
        }
    }

    public async Task<Result<PaginatedList<SiteDto>>> GetPaginatedAsync(GetSitesQueryDto query)
    {
        try
        {
            var sitesQuery = _context.Sites
                .Include(s => s.SiteManager)
                .Include(s => s.Company)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLower = query.Search.ToLower();
                sitesQuery = sitesQuery.Where(s =>
                    s.SiteCode.ToLower().Contains(searchLower) ||
                    s.SiteName.ToLower().Contains(searchLower) ||
                    (s.City != null && s.City.ToLower().Contains(searchLower)) ||
                    (s.Address != null && s.Address.ToLower().Contains(searchLower))
                );
            }

            // Apply sorting
            sitesQuery = ApplySorting(sitesQuery, query.SortColumn, query.SortDirection);

            // Get total count before pagination
            var totalCount = await sitesQuery.CountAsync();

            // Apply pagination
            var sites = await sitesQuery
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(s => new SiteDto(
                    s.Id,
                    s.SiteCode,
                    s.SiteName,
                    s.Address,
                    s.City,
                    s.PostalCode,
                    s.SiteManagerId,
                    s.SiteManager != null ? s.SiteManager.FirstName + " " + s.SiteManager.LastName : null,
                    s.CompanyId,
                    s.Company != null ? s.Company.CompanyName : null,
                    s.Phone,
                    s.Email,
                    s.IsActive,
                    s.Notes,
                    s.Latitude,
                    s.Longitude,
                    s.GeofenceRadiusMeters,
                    s.FloatProjectId,
                    s.FloatLinkedAt,
                    s.FloatLinkMethod
                ))
                .ToListAsync();

            var result = new PaginatedList<SiteDto>(sites, totalCount, query.PageNumber, query.PageSize);
            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            return Result.Fail<PaginatedList<SiteDto>>($"Error retrieving sites: {ex.Message}");
        }
    }

    private static IQueryable<Site> ApplySorting(IQueryable<Site> query, string? sortColumn, string? sortDirection)
    {
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortColumn?.ToLower() switch
        {
            "sitecode" => isDescending ? query.OrderByDescending(s => s.SiteCode) : query.OrderBy(s => s.SiteCode),
            "sitename" => isDescending ? query.OrderByDescending(s => s.SiteName) : query.OrderBy(s => s.SiteName),
            "city" => isDescending ? query.OrderByDescending(s => s.City) : query.OrderBy(s => s.City),
            "sitemanagername" => isDescending
                ? query.OrderByDescending(s => s.SiteManager != null ? s.SiteManager.FirstName + " " + s.SiteManager.LastName : "")
                : query.OrderBy(s => s.SiteManager != null ? s.SiteManager.FirstName + " " + s.SiteManager.LastName : ""),
            "companyname" => isDescending
                ? query.OrderByDescending(s => s.Company != null ? s.Company.CompanyName : "")
                : query.OrderBy(s => s.Company != null ? s.Company.CompanyName : ""),
            "isactive" => isDescending ? query.OrderByDescending(s => s.IsActive) : query.OrderBy(s => s.IsActive),
            _ => query.OrderBy(s => s.SiteCode)
        };
    }

    public async Task<Result<SiteDto>> GetByIdAsync(Guid id)
    {
        try
        {
            var site = await _context.Sites
                .Include(s => s.SiteManager)
                .Include(s => s.Company)
                .Where(s => s.Id == id)
                .Select(s => new SiteDto(
                    s.Id,
                    s.SiteCode,
                    s.SiteName,
                    s.Address,
                    s.City,
                    s.PostalCode,
                    s.SiteManagerId,
                    s.SiteManager != null ? s.SiteManager.FirstName + " " + s.SiteManager.LastName : null,
                    s.CompanyId,
                    s.Company != null ? s.Company.CompanyName : null,
                    s.Phone,
                    s.Email,
                    s.IsActive,
                    s.Notes,
                    s.Latitude,
                    s.Longitude,
                    s.GeofenceRadiusMeters,
                    s.FloatProjectId,
                    s.FloatLinkedAt,
                    s.FloatLinkMethod
                ))
                .FirstOrDefaultAsync();

            if (site == null)
            {
                return Result.Fail<SiteDto>($"Site with ID {id} not found");
            }

            return Result.Ok(site);
        }
        catch (Exception ex)
        {
            return Result.Fail<SiteDto>($"Error retrieving site: {ex.Message}");
        }
    }

    public async Task<Result<SiteDto>> CreateAsync(CreateSiteDto dto)
    {
        try
        {
            // Validate that SiteManagerId exists if provided
            if (dto.SiteManagerId.HasValue)
            {
                var managerExists = await _context.Employees
                    .AnyAsync(e => e.Id == dto.SiteManagerId.Value);

                if (!managerExists)
                {
                    return Result.Fail<SiteDto>($"Employee with ID {dto.SiteManagerId} not found");
                }
            }

            // Validate that CompanyId exists if provided
            if (dto.CompanyId.HasValue)
            {
                var companyExists = await _context.Companies
                    .AnyAsync(c => c.Id == dto.CompanyId.Value);

                if (!companyExists)
                {
                    return Result.Fail<SiteDto>($"Company with ID {dto.CompanyId} not found");
                }
            }

            // Check for duplicate SiteCode within the same tenant
            var duplicateCode = await _context.Sites
                .AnyAsync(s => s.SiteCode == dto.SiteCode);

            if (duplicateCode)
            {
                return Result.Fail<SiteDto>($"Site with code '{dto.SiteCode}' already exists");
            }

            var site = new Site
            {
                Id = Guid.NewGuid(),
                SiteCode = dto.SiteCode,
                SiteName = dto.SiteName,
                Address = dto.Address,
                City = dto.City,
                PostalCode = dto.PostalCode,
                SiteManagerId = dto.SiteManagerId,
                CompanyId = dto.CompanyId,
                Phone = dto.Phone,
                Email = dto.Email,
                IsActive = dto.IsActive,
                Notes = dto.Notes,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                GeofenceRadiusMeters = dto.GeofenceRadiusMeters,
                FloatProjectId = dto.FloatProjectId,
                FloatLinkedAt = dto.FloatProjectId.HasValue ? DateTime.UtcNow : null,
                FloatLinkMethod = dto.FloatProjectId.HasValue ? "Manual" : null
            };

            _context.Sites.Add(site);
            await _context.SaveChangesAsync();

            // Reload with related entities to get names
            var createdSite = await _context.Sites
                .Include(s => s.SiteManager)
                .Include(s => s.Company)
                .FirstAsync(s => s.Id == site.Id);

            var siteDto = new SiteDto(
                createdSite.Id,
                createdSite.SiteCode,
                createdSite.SiteName,
                createdSite.Address,
                createdSite.City,
                createdSite.PostalCode,
                createdSite.SiteManagerId,
                createdSite.SiteManager != null ? createdSite.SiteManager.FirstName + " " + createdSite.SiteManager.LastName : null,
                createdSite.CompanyId,
                createdSite.Company?.CompanyName,
                createdSite.Phone,
                createdSite.Email,
                createdSite.IsActive,
                createdSite.Notes,
                createdSite.Latitude,
                createdSite.Longitude,
                createdSite.GeofenceRadiusMeters,
                createdSite.FloatProjectId,
                createdSite.FloatLinkedAt,
                createdSite.FloatLinkMethod
            );

            return Result.Ok(siteDto);
        }
        catch (Exception ex)
        {
            return Result.Fail<SiteDto>($"Error creating site: {ex.Message}");
        }
    }

    public async Task<Result<SiteDto>> UpdateAsync(Guid id, UpdateSiteDto dto)
    {
        try
        {
            var site = await _context.Sites
                .Include(s => s.SiteManager)
                .Include(s => s.Company)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (site == null)
            {
                return Result.Fail<SiteDto>($"Site with ID {id} not found");
            }

            // Validate that SiteManagerId exists if provided
            if (dto.SiteManagerId.HasValue)
            {
                var managerExists = await _context.Employees
                    .AnyAsync(e => e.Id == dto.SiteManagerId.Value);

                if (!managerExists)
                {
                    return Result.Fail<SiteDto>($"Employee with ID {dto.SiteManagerId} not found");
                }
            }

            // Validate that CompanyId exists if provided
            if (dto.CompanyId.HasValue)
            {
                var companyExists = await _context.Companies
                    .AnyAsync(c => c.Id == dto.CompanyId.Value);

                if (!companyExists)
                {
                    return Result.Fail<SiteDto>($"Company with ID {dto.CompanyId} not found");
                }
            }

            // Check for duplicate SiteCode (excluding current site)
            var duplicateCode = await _context.Sites
                .AnyAsync(s => s.SiteCode == dto.SiteCode && s.Id != id);

            if (duplicateCode)
            {
                return Result.Fail<SiteDto>($"Site with code '{dto.SiteCode}' already exists");
            }

            site.SiteCode = dto.SiteCode;
            site.SiteName = dto.SiteName;
            site.Address = dto.Address;
            site.City = dto.City;
            site.PostalCode = dto.PostalCode;
            site.SiteManagerId = dto.SiteManagerId;
            site.CompanyId = dto.CompanyId;
            site.Phone = dto.Phone;
            site.Email = dto.Email;
            site.IsActive = dto.IsActive;
            site.Notes = dto.Notes;
            site.Latitude = dto.Latitude;
            site.Longitude = dto.Longitude;
            site.GeofenceRadiusMeters = dto.GeofenceRadiusMeters;

            // Handle FloatProjectId changes
            if (dto.FloatProjectId != site.FloatProjectId)
            {
                if (dto.FloatProjectId.HasValue)
                {
                    // Setting or changing FloatProjectId
                    site.FloatProjectId = dto.FloatProjectId;
                    site.FloatLinkedAt = DateTime.UtcNow;
                    site.FloatLinkMethod = "Manual";
                }
                else
                {
                    // Clearing FloatProjectId
                    site.FloatProjectId = null;
                    site.FloatLinkedAt = null;
                    site.FloatLinkMethod = null;
                }
            }

            await _context.SaveChangesAsync();

            // Reload to get updated related entity names
            await _context.Sites
                .Entry(site)
                .Reference(s => s.SiteManager)
                .LoadAsync();

            await _context.Sites
                .Entry(site)
                .Reference(s => s.Company)
                .LoadAsync();

            var siteDto = new SiteDto(
                site.Id,
                site.SiteCode,
                site.SiteName,
                site.Address,
                site.City,
                site.PostalCode,
                site.SiteManagerId,
                site.SiteManager != null ? site.SiteManager.FirstName + " " + site.SiteManager.LastName : null,
                site.CompanyId,
                site.Company?.CompanyName,
                site.Phone,
                site.Email,
                site.IsActive,
                site.Notes,
                site.Latitude,
                site.Longitude,
                site.GeofenceRadiusMeters,
                site.FloatProjectId,
                site.FloatLinkedAt,
                site.FloatLinkMethod
            );

            return Result.Ok(siteDto);
        }
        catch (Exception ex)
        {
            return Result.Fail<SiteDto>($"Error updating site: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        try
        {
            var site = await _context.Sites
                .FirstOrDefaultAsync(s => s.Id == id);

            if (site == null)
            {
                return Result.Fail($"Site with ID {id} not found");
            }

            site.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Error deleting site: {ex.Message}");
        }
    }
}
