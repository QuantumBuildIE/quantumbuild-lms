using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Sectors;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Standards;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Standards;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Standards;

public class TenantStandardSubscriptionService(
    IToolboxTalksDbContext dbContext,
    ITenantSectorService tenantSectorService,
    ILogger<TenantStandardSubscriptionService> logger) : ITenantStandardSubscriptionService
{
    public async Task<List<AvailableStandardDto>> GetAvailableStandardsAsync(
        Guid tenantId, bool includeCrossSector, CancellationToken cancellationToken = default)
    {
        var tenantSectorIds = (await tenantSectorService.GetTenantSectorsAsync(tenantId, cancellationToken))
            .Select(ts => ts.SectorId)
            .ToHashSet();

        var subscribedBodyIds = await dbContext.TenantStandardSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => s.RegulatoryBodyId)
            .ToListAsync(cancellationToken);
        var subscribedSet = subscribedBodyIds.ToHashSet();

        var query = dbContext.RegulatoryBodies
            .Include(b => b.Sector)
            .Where(b => b.Kind == RegulatoryBodyKind.Standard);

        if (!includeCrossSector)
            query = query.Where(b => b.SectorId != null && tenantSectorIds.Contains(b.SectorId.Value));

        var bodies = await query
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);

        return bodies
            .Select(b => new AvailableStandardDto
            {
                Id = b.Id,
                Name = b.Name,
                Code = b.Code,
                Country = b.Country,
                SectorId = b.SectorId!.Value,
                SectorName = b.Sector?.Name ?? string.Empty,
                IsSubscribed = subscribedSet.Contains(b.Id),
                IsCrossSector = !tenantSectorIds.Contains(b.SectorId.Value)
            })
            .ToList();
    }

    public async Task<List<TenantStandardSubscriptionDto>> GetSubscribedStandardsAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenantSectorIds = (await tenantSectorService.GetTenantSectorsAsync(tenantId, cancellationToken))
            .Select(ts => ts.SectorId)
            .ToHashSet();

        return await dbContext.TenantStandardSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Include(s => s.RegulatoryBody)
            .ThenInclude(b => b.Sector)
            .OrderBy(s => s.RegulatoryBody.Name)
            .Select(s => new TenantStandardSubscriptionDto
            {
                Id = s.Id,
                TenantId = s.TenantId,
                RegulatoryBodyId = s.RegulatoryBodyId,
                Name = s.RegulatoryBody.Name,
                Code = s.RegulatoryBody.Code,
                Country = s.RegulatoryBody.Country,
                SectorId = s.RegulatoryBody.SectorId!.Value,
                SectorName = s.RegulatoryBody.Sector != null ? s.RegulatoryBody.Sector.Name : string.Empty,
                IsCrossSector = s.RegulatoryBody.SectorId == null || !tenantSectorIds.Contains(s.RegulatoryBody.SectorId.Value),
                SubscribedAt = s.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantStandardSubscriptionDto> SubscribeAsync(
        Guid tenantId, Guid regulatoryBodyId, CancellationToken cancellationToken = default)
    {
        var body = await dbContext.RegulatoryBodies
            .Include(b => b.Sector)
            .FirstOrDefaultAsync(b => b.Id == regulatoryBodyId, cancellationToken);

        if (body == null)
            throw new InvalidOperationException("Regulatory body not found.");

        if (body.Kind != RegulatoryBodyKind.Standard)
            throw new InvalidOperationException("Tenants can only subscribe to regulatory bodies with Kind = Standard.");

        var tenantSectorIds = (await tenantSectorService.GetTenantSectorsAsync(tenantId, cancellationToken))
            .Select(ts => ts.SectorId)
            .ToHashSet();
        var isCrossSector = body.SectorId == null || !tenantSectorIds.Contains(body.SectorId.Value);

        // Check for existing assignment (including soft-deleted for restore-on-reassign) — the
        // unique index on {TenantId, RegulatoryBodyId} is not filtered by IsDeleted.
        var existing = await dbContext.TenantStandardSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.RegulatoryBodyId == regulatoryBodyId, cancellationToken);

        if (existing != null)
        {
            if (!existing.IsDeleted)
                throw new InvalidOperationException("Tenant is already subscribed to this standard.");

            existing.IsDeleted = false;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Restored soft-deleted TenantStandardSubscription: TenantId={TenantId}, RegulatoryBodyId={RegulatoryBodyId}",
                tenantId, regulatoryBodyId);

            return MapToDto(existing, body, isCrossSector);
        }

        var subscription = TenantStandardSubscription.Create(tenantId, body);
        dbContext.TenantStandardSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created TenantStandardSubscription: TenantId={TenantId}, RegulatoryBodyId={RegulatoryBodyId}",
            tenantId, regulatoryBodyId);

        return MapToDto(subscription, body, isCrossSector);
    }

    public async Task UnsubscribeAsync(Guid tenantId, Guid regulatoryBodyId, CancellationToken cancellationToken = default)
    {
        var subscription = await dbContext.TenantStandardSubscriptions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.RegulatoryBodyId == regulatoryBodyId && !s.IsDeleted, cancellationToken);

        if (subscription == null)
            throw new InvalidOperationException("Subscription not found.");

        subscription.IsDeleted = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Removed TenantStandardSubscription: TenantId={TenantId}, RegulatoryBodyId={RegulatoryBodyId}",
            tenantId, regulatoryBodyId);
    }

    private static TenantStandardSubscriptionDto MapToDto(TenantStandardSubscription subscription, RegulatoryBody body, bool isCrossSector) => new()
    {
        Id = subscription.Id,
        TenantId = subscription.TenantId,
        RegulatoryBodyId = subscription.RegulatoryBodyId,
        Name = body.Name,
        Code = body.Code,
        Country = body.Country,
        SectorId = body.SectorId!.Value,
        SectorName = body.Sector?.Name ?? string.Empty,
        IsCrossSector = isCrossSector,
        SubscribedAt = subscription.CreatedAt
    };
}
