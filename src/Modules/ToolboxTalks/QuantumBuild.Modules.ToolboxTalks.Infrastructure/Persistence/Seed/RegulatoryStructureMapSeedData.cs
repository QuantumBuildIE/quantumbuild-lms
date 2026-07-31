using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory.StructureMaps;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds the DB-backed RegulatoryStructureMap tree for HIQA from the authored content in
/// HiqaStructureMap.Principles (the seed source — see that class's doc comment). Writes a single
/// Draft map, idempotently: if a RegulatoryStructureMap row already exists for the HIQA document,
/// this is a no-op — it never overwrites content a human may since have edited or verified.
/// </summary>
public static class RegulatoryStructureMapSeedData
{
    public static async Task SeedAsync(DbContext context, ILogger logger)
    {
        var now = DateTime.UtcNow;

        var hiqaBody = await context.Set<RegulatoryBody>()
            .IgnoreQueryFilters()
            .Where(b => !b.IsDeleted && b.Code == HiqaStructureMap.RegulatoryBodyCode)
            .FirstOrDefaultAsync();

        if (hiqaBody == null)
        {
            logger.LogWarning("HIQA regulatory body not found — skipping structure map seeding");
            return;
        }

        var hiqaDoc = await context.Set<RegulatoryDocument>()
            .IgnoreQueryFilters()
            .Where(d => !d.IsDeleted && d.RegulatoryBodyId == hiqaBody.Id)
            .FirstOrDefaultAsync();

        if (hiqaDoc == null)
        {
            logger.LogWarning("HIQA regulatory document not found — skipping structure map seeding");
            return;
        }

        var existingMap = await context.Set<RegulatoryStructureMap>()
            .IgnoreQueryFilters()
            .Where(m => !m.IsDeleted && m.RegulatoryDocumentId == hiqaDoc.Id)
            .FirstOrDefaultAsync();

        if (existingMap != null)
        {
            logger.LogInformation("HIQA structure map already exists, skipping");
            return;
        }

        var map = new RegulatoryStructureMap
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = hiqaDoc.Id,
            Status = Domain.Enums.RegulatoryStructureMapStatus.Draft,
            CreatedAt = now,
            CreatedBy = "system"
        };

        var principleDisplayOrder = 0;
        foreach (var sourcePrinciple in HiqaStructureMap.Principles)
        {
            var principle = new RegulatoryStructureMapPrinciple
            {
                Id = Guid.NewGuid(),
                RegulatoryStructureMapId = map.Id,
                Number = sourcePrinciple.Number,
                DisplayOrder = principleDisplayOrder++,
                CreatedAt = now,
                CreatedBy = "system"
            };
            map.Principles.Add(principle);

            var standardDisplayOrder = 0;
            foreach (var sourceStandard in sourcePrinciple.Standards)
            {
                var standard = new RegulatoryStructureMapStandard
                {
                    Id = Guid.NewGuid(),
                    RegulatoryStructureMapPrincipleId = principle.Id,
                    StandardId = sourceStandard.Id,
                    DisplayOrder = standardDisplayOrder++,
                    CreatedAt = now,
                    CreatedBy = "system"
                };
                principle.Standards.Add(standard);

                var featureDisplayOrder = 0;
                foreach (var sourceFeature in sourceStandard.Features)
                {
                    standard.Features.Add(new RegulatoryStructureMapFeature
                    {
                        Id = Guid.NewGuid(),
                        RegulatoryStructureMapStandardId = standard.Id,
                        Identifier = sourceFeature.Identifier,
                        Block = sourceFeature.Block,
                        VerbatimText = sourceFeature.VerbatimText,
                        FootnoteDefinition = sourceFeature.FootnoteDefinition,
                        DisplayOrder = featureDisplayOrder++,
                        CreatedAt = now,
                        CreatedBy = "system"
                    });
                }
            }
        }

        await context.Set<RegulatoryStructureMap>().AddAsync(map);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Seeded HIQA structure map (Draft): {PrincipleCount} principles, {StandardCount} standards, {FeatureCount} features",
            map.Principles.Count,
            map.Principles.Sum(p => p.Standards.Count),
            map.Principles.Sum(p => p.Standards.Sum(s => s.Features.Count)));
    }
}
