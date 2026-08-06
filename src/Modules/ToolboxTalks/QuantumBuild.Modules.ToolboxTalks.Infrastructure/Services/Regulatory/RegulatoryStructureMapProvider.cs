using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory;

/// <summary>
/// DB-backed dispatch: resolves the RegulatoryStructureMap row for a RegulatoryDocumentId (see
/// IRegulatoryStructureMapProvider for the keying rationale) and projects it, plus its Principle/
/// Standard/Feature children, into the Application-layer DocumentStructureMap record consumers
/// work with.
///
/// Deliberately reads with flat per-level queries rather than EF Include() chains — each level's
/// row count for a single document is small (tens to low hundreds), assembly happens in memory via
/// ToLookup, and avoiding Include keeps this simple to unit test against a mocked
/// IToolboxTalksDbContext (the codebase's established DbSet-mocking test pattern does not support
/// Include-based navigation).
/// </summary>
public sealed class RegulatoryStructureMapProvider : IRegulatoryStructureMapProvider
{
    private readonly IToolboxTalksDbContext _dbContext;

    public RegulatoryStructureMapProvider(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(bool Found, DocumentStructureMap? Map)> TryGetStructureMapAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default)
    {
        var mapRow = await _dbContext.RegulatoryStructureMaps
            .Where(m => m.RegulatoryDocumentId == regulatoryDocumentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (mapRow is null)
        {
            return (false, null);
        }

        var map = await ProjectAsync(mapRow, cancellationToken);
        return (true, map);
    }

    public async Task<DocumentStructureMap> GetStructureMapAsync(
        Guid regulatoryDocumentId,
        CancellationToken cancellationToken = default)
    {
        var (found, map) = await TryGetStructureMapAsync(regulatoryDocumentId, cancellationToken);

        if (!found || map is null)
        {
            var title = await _dbContext.RegulatoryDocuments
                .Where(d => d.Id == regulatoryDocumentId)
                .Select(d => d.Title)
                .FirstOrDefaultAsync(cancellationToken) ?? "(unknown document)";

            throw new RegulatoryStructureMapNotFoundException(regulatoryDocumentId, title);
        }

        return map;
    }

    private async Task<DocumentStructureMap> ProjectAsync(
        RegulatoryStructureMap mapRow,
        CancellationToken cancellationToken)
    {
        var principleRows = await _dbContext.RegulatoryStructureMapPrinciples
            .Where(p => p.RegulatoryStructureMapId == mapRow.Id)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(cancellationToken);

        var principleIds = principleRows.Select(p => p.Id).ToList();

        var standardRows = await _dbContext.RegulatoryStructureMapStandards
            .Where(s => principleIds.Contains(s.RegulatoryStructureMapPrincipleId))
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(cancellationToken);

        var standardIds = standardRows.Select(s => s.Id).ToList();

        var featureRows = await _dbContext.RegulatoryStructureMapFeatures
            .Where(f => standardIds.Contains(f.RegulatoryStructureMapStandardId))
            .OrderBy(f => f.DisplayOrder)
            .ToListAsync(cancellationToken);

        var standardsByPrinciple = standardRows.ToLookup(s => s.RegulatoryStructureMapPrincipleId);
        var featuresByStandard = featureRows.ToLookup(f => f.RegulatoryStructureMapStandardId);

        var principles = principleRows
            .Select(p => new StructurePrinciple(
                p.Number,
                standardsByPrinciple[p.Id]
                    .Select(s => new StructureStandard(
                        s.StandardId,
                        featuresByStandard[s.Id]
                            .Select(f => new StructureFeature(f.Identifier, f.Block, f.VerbatimText, f.FootnoteDefinition))
                            .ToList()))
                    .ToList()))
            .ToList();

        return new DocumentStructureMap(
            mapRow.Id,
            mapRow.RegulatoryDocumentId,
            mapRow.Status,
            mapRow.VerifiedBy,
            mapRow.VerifiedAt,
            principles);
    }
}
