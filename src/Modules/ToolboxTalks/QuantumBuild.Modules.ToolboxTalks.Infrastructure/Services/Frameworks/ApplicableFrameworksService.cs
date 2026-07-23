using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Frameworks;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Frameworks;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Frameworks;

public class ApplicableFrameworksService(IToolboxTalksDbContext dbContext) : IApplicableFrameworksService
{
    public async Task<TenantEntitlementsDto> GetTenantEntitlementsAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sectorKeys = await dbContext.TenantSectors
            .Where(ts => ts.TenantId == tenantId && !ts.IsDeleted)
            .Select(ts => ts.Sector.Key)
            .ToListAsync(cancellationToken);

        var subscribedBodyIds = await dbContext.TenantStandardSubscriptions
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId && !s.IsDeleted)
            .Select(s => s.RegulatoryBodyId)
            .ToListAsync(cancellationToken);

        return new TenantEntitlementsDto(sectorKeys, subscribedBodyIds);
    }

    public async Task<List<ApplicableFrameworkDto>> GetApplicableFrameworksAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entitlements = await GetTenantEntitlementsAsync(tenantId, cancellationToken);
        var result = new List<ApplicableFrameworkDto>();

        if (entitlements.SectorKeys.Count > 0)
        {
            var regulationRows = await dbContext.RegulatoryRequirements
                .IgnoreQueryFilters()
                .Where(r => !r.IsDeleted && r.IsActive
                    && r.IngestionStatus == RequirementIngestionStatus.Approved
                    && entitlements.SectorKeys.Contains(r.RegulatoryProfile.SectorKey)
                    && r.RegulatoryProfile.RegulatoryDocument.RegulatoryBody.Kind == RegulatoryBodyKind.Regulation)
                .GroupBy(r => new
                {
                    BodyId = r.RegulatoryProfile.RegulatoryDocument.RegulatoryBodyId,
                    BodyName = r.RegulatoryProfile.RegulatoryDocument.RegulatoryBody.Name,
                    BodyCode = r.RegulatoryProfile.RegulatoryDocument.RegulatoryBody.Code,
                    SectorKey = r.RegulatoryProfile.SectorKey,
                    SectorName = r.RegulatoryProfile.Sector.Name,
                })
                .Select(g => new ApplicableFrameworkDto(
                    g.Key.BodyId, g.Key.BodyName, g.Key.BodyCode, nameof(RegulatoryBodyKind.Regulation),
                    g.Key.SectorKey, g.Key.SectorName, "Sector", g.Count()))
                .ToListAsync(cancellationToken);

            result.AddRange(regulationRows);
        }

        if (entitlements.SubscribedStandardBodyIds.Count > 0)
        {
            var standardRows = await dbContext.RegulatoryRequirements
                .IgnoreQueryFilters()
                .Where(r => !r.IsDeleted && r.IsActive
                    && r.IngestionStatus == RequirementIngestionStatus.Approved
                    && entitlements.SubscribedStandardBodyIds.Contains(r.RegulatoryProfile.RegulatoryDocument.RegulatoryBodyId))
                .GroupBy(r => new
                {
                    BodyId = r.RegulatoryProfile.RegulatoryDocument.RegulatoryBodyId,
                    BodyName = r.RegulatoryProfile.RegulatoryDocument.RegulatoryBody.Name,
                    BodyCode = r.RegulatoryProfile.RegulatoryDocument.RegulatoryBody.Code,
                    SectorKey = r.RegulatoryProfile.SectorKey,
                    SectorName = r.RegulatoryProfile.Sector.Name,
                })
                .Select(g => new ApplicableFrameworkDto(
                    g.Key.BodyId, g.Key.BodyName, g.Key.BodyCode, nameof(RegulatoryBodyKind.Standard),
                    g.Key.SectorKey, g.Key.SectorName, "Subscription", g.Count()))
                .ToListAsync(cancellationToken);

            result.AddRange(standardRows);

            // Subscribed Standards with zero approved requirements so far still appear —
            // the tenant subscribed to them and should see that reflected even before content exists.
            var bodiesWithRows = standardRows.Select(r => r.RegulatoryBodyId).ToHashSet();
            var emptyBodyIds = entitlements.SubscribedStandardBodyIds
                .Where(id => !bodiesWithRows.Contains(id))
                .ToList();

            if (emptyBodyIds.Count > 0)
            {
                var emptyBodies = await dbContext.RegulatoryBodies
                    .Include(b => b.Sector)
                    .Where(b => emptyBodyIds.Contains(b.Id))
                    .ToListAsync(cancellationToken);

                result.AddRange(emptyBodies.Select(b => new ApplicableFrameworkDto(
                    b.Id, b.Name, b.Code, nameof(RegulatoryBodyKind.Standard),
                    b.Sector?.Key ?? string.Empty, b.Sector?.Name ?? string.Empty, "Subscription", 0)));
            }
        }

        return result.OrderBy(f => f.Kind).ThenBy(f => f.BodyName).ToList();
    }
}
