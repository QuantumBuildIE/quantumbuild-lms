using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Sectors;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Sectors;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Sectors;

public class TenantSectorService(
    IToolboxTalksDbContext dbContext,
    ICoreDbContext coreDbContext,
    ILogger<TenantSectorService> logger) : ITenantSectorService
{
    public async Task<List<TenantSectorDto>> GetTenantSectorsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.TenantSectors
            .IgnoreQueryFilters()
            .Where(ts => ts.TenantId == tenantId && !ts.IsDeleted)
            .Include(ts => ts.Sector)
            .OrderBy(ts => ts.Sector.DisplayOrder)
            .Select(ts => new TenantSectorDto
            {
                Id = ts.Id,
                TenantId = ts.TenantId,
                SectorId = ts.SectorId,
                SectorKey = ts.Sector.Key,
                SectorName = ts.Sector.Name,
                SectorIcon = ts.Sector.Icon,
                IsDefault = ts.IsDefault
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantSectorDto> AssignSectorAsync(Guid tenantId, Guid sectorId, bool isDefault, CancellationToken cancellationToken = default)
    {
        // Validate tenant exists
        var tenantExists = await coreDbContext.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == tenantId && !t.IsDeleted, cancellationToken);

        if (!tenantExists)
            throw new InvalidOperationException("Tenant not found.");

        // Validate sector exists and is active
        var sector = await dbContext.Sectors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sectorId && s.IsActive && !s.IsDeleted, cancellationToken);

        if (sector == null)
            throw new InvalidOperationException("Sector not found or is inactive.");

        // Check for existing assignment (including soft-deleted for restore-on-reassign)
        var existing = await dbContext.TenantSectors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ts => ts.TenantId == tenantId && ts.SectorId == sectorId, cancellationToken);

        if (existing != null)
        {
            if (!existing.IsDeleted)
                throw new InvalidOperationException("CONFLICT:Sector is already assigned to this tenant.");

            // Restore soft-deleted record (restore-on-reassign pattern)
            existing.IsDeleted = false;
            existing.IsDefault = isDefault;

            // If setting as default, clear IsDefault on all others
            if (isDefault)
                await ClearDefaultAsync(tenantId, existing.Id, cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Restored soft-deleted TenantSector assignment: TenantId={TenantId}, SectorId={SectorId}",
                tenantId, sectorId);

            return MapToDto(existing, sector);
        }

        // If setting as default, clear IsDefault on all others first
        if (isDefault)
            await ClearDefaultAsync(tenantId, null, cancellationToken);

        var tenantSector = new TenantSector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SectorId = sectorId,
            IsDefault = isDefault
        };

        dbContext.TenantSectors.Add(tenantSector);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Assigned sector to tenant: TenantId={TenantId}, SectorId={SectorId}, IsDefault={IsDefault}",
            tenantId, sectorId, isDefault);

        return MapToDto(tenantSector, sector);
    }

    public async Task RemoveSectorAsync(Guid tenantId, Guid sectorId, CancellationToken cancellationToken = default)
    {
        var tenantSector = await dbContext.TenantSectors
            .IgnoreQueryFilters()
            .Include(ts => ts.Sector)
            .FirstOrDefaultAsync(ts => ts.TenantId == tenantId && ts.SectorId == sectorId && !ts.IsDeleted, cancellationToken);

        if (tenantSector == null)
            throw new InvalidOperationException("Sector is not assigned to this tenant.");

        // Validate at least one other active sector remains
        var activeCount = await dbContext.TenantSectors
            .IgnoreQueryFilters()
            .CountAsync(ts => ts.TenantId == tenantId && !ts.IsDeleted, cancellationToken);

        if (activeCount <= 1)
            throw new InvalidOperationException("VALIDATION:Cannot remove the last sector. A tenant must have at least one active sector.");

        var wasDefault = tenantSector.IsDefault;

        // Soft delete
        tenantSector.IsDeleted = true;
        tenantSector.IsDefault = false;

        // If deleted sector was default, promote the remaining sector with lowest DisplayOrder
        if (wasDefault)
        {
            var newDefault = await dbContext.TenantSectors
                .IgnoreQueryFilters()
                .Include(ts => ts.Sector)
                .Where(ts => ts.TenantId == tenantId && !ts.IsDeleted && ts.Id != tenantSector.Id)
                .OrderBy(ts => ts.Sector.DisplayOrder)
                .FirstOrDefaultAsync(cancellationToken);

            if (newDefault != null)
                newDefault.IsDefault = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Removed sector from tenant: TenantId={TenantId}, SectorId={SectorId}, WasDefault={WasDefault}",
            tenantId, sectorId, wasDefault);
    }

    public async Task SetDefaultSectorAsync(Guid tenantId, Guid sectorId, CancellationToken cancellationToken = default)
    {
        var tenantSector = await dbContext.TenantSectors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(ts => ts.TenantId == tenantId && ts.SectorId == sectorId && !ts.IsDeleted, cancellationToken);

        if (tenantSector == null)
            throw new InvalidOperationException("Sector is not actively assigned to this tenant.");

        // Clear IsDefault on all others
        await ClearDefaultAsync(tenantId, tenantSector.Id, cancellationToken);

        tenantSector.IsDefault = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Set default sector for tenant: TenantId={TenantId}, SectorId={SectorId}",
            tenantId, sectorId);
    }

    public async Task<TenantSectorDto?> GetDefaultSectorAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // First try: sector explicitly marked as default
        var defaultSector = await dbContext.TenantSectors
            .IgnoreQueryFilters()
            .Include(ts => ts.Sector)
            .Where(ts => ts.TenantId == tenantId && !ts.IsDeleted && ts.IsDefault)
            .Select(ts => new TenantSectorDto
            {
                Id = ts.Id,
                TenantId = ts.TenantId,
                SectorId = ts.SectorId,
                SectorKey = ts.Sector.Key,
                SectorName = ts.Sector.Name,
                SectorIcon = ts.Sector.Icon,
                IsDefault = ts.IsDefault
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (defaultSector != null)
            return defaultSector;

        // Fallback: if only one active sector exists, return it
        var activeSectors = await dbContext.TenantSectors
            .IgnoreQueryFilters()
            .Include(ts => ts.Sector)
            .Where(ts => ts.TenantId == tenantId && !ts.IsDeleted)
            .Select(ts => new TenantSectorDto
            {
                Id = ts.Id,
                TenantId = ts.TenantId,
                SectorId = ts.SectorId,
                SectorKey = ts.Sector.Key,
                SectorName = ts.Sector.Name,
                SectorIcon = ts.Sector.Icon,
                IsDefault = ts.IsDefault
            })
            .ToListAsync(cancellationToken);

        // Single sector fallback — return it as the implicit default
        if (activeSectors.Count == 1)
            return activeSectors[0];

        // Multiple sectors with no default set — ambiguous, return null
        return null;
    }

    private async Task ClearDefaultAsync(Guid tenantId, Guid? excludeId, CancellationToken cancellationToken)
    {
        var currentDefaults = await dbContext.TenantSectors
            .IgnoreQueryFilters()
            .Where(ts => ts.TenantId == tenantId && !ts.IsDeleted && ts.IsDefault)
            .Where(ts => excludeId == null || ts.Id != excludeId)
            .ToListAsync(cancellationToken);

        foreach (var ts in currentDefaults)
            ts.IsDefault = false;
    }

    private static TenantSectorDto MapToDto(TenantSector tenantSector, Sector sector) => new()
    {
        Id = tenantSector.Id,
        TenantId = tenantSector.TenantId,
        SectorId = tenantSector.SectorId,
        SectorKey = sector.Key,
        SectorName = sector.Name,
        SectorIcon = sector.Icon,
        IsDefault = tenantSector.IsDefault
    };
}
