using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory.StructureMaps;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds the DB-backed RegulatoryStructureMap tree for each authored seed source (HIQA, RSA) from
/// its static content (see <see cref="HiqaStructureMap"/> / <see cref="RsaStructureMap"/>). Each
/// source declares a body code AND an exact document title; the map binds only to the single
/// non-deleted document of that body with that exact title. Zero matches logs a Warning and skips;
/// more than one logs an Error and skips — it never picks one arbitrarily. Writes a Draft map,
/// idempotently: if a map already exists for the bound document this is a no-op, so it never
/// overwrites content a human may since have edited or verified.
/// </summary>
public static class RegulatoryStructureMapSeedData
{
    public static IReadOnlyList<StructureMapSeedSource> Sources { get; } =
        new[] { HiqaStructureMap.Source, RsaStructureMap.Source };

    public static Task SeedAsync(DbContext context, ILogger logger) =>
        SeedAsync(context, logger, Sources);

    public static async Task SeedAsync(
        DbContext context,
        ILogger logger,
        IEnumerable<StructureMapSeedSource> sources)
    {
        foreach (var source in sources)
        {
            await SeedSourceAsync(context, logger, source);
        }
    }

    private static async Task SeedSourceAsync(DbContext context, ILogger logger, StructureMapSeedSource source)
    {
        var now = DateTime.UtcNow;

        var bodies = await context.Set<RegulatoryBody>()
            .IgnoreQueryFilters()
            .Where(b => !b.IsDeleted && b.Code == source.RegulatoryBodyCode)
            .ToListAsync();

        if (bodies.Count == 0)
        {
            logger.LogWarning(
                "{BodyCode} regulatory body not found — skipping structure map seeding",
                source.RegulatoryBodyCode);
            return;
        }

        if (bodies.Count > 1)
        {
            logger.LogError(
                "{BodyCode} structure map not bound: {Count} regulatory bodies share this code — skipping",
                source.RegulatoryBodyCode, bodies.Count);
            return;
        }

        var bodyId = bodies[0].Id;

        var documents = await context.Set<RegulatoryDocument>()
            .IgnoreQueryFilters()
            .Where(d => !d.IsDeleted && d.RegulatoryBodyId == bodyId && d.Title == source.DocumentTitle)
            .ToListAsync();

        if (documents.Count == 0)
        {
            logger.LogWarning(
                "{BodyCode} structure map not bound: no regulatory document titled '{Title}' — skipping",
                source.RegulatoryBodyCode, source.DocumentTitle);
            return;
        }

        var terminology = source.EffectiveTerminology;

        // A map on any matching document means this source is already bound (Dev has duplicate
        // same-title documents, one of which carries the map). Keep the labels current, never touch
        // content or verification state, and stay quiet.
        var documentIds = documents.Select(d => d.Id).ToList();
        var existingMaps = await context.Set<RegulatoryStructureMap>()
            .IgnoreQueryFilters()
            .Where(m => !m.IsDeleted && documentIds.Contains(m.RegulatoryDocumentId))
            .ToListAsync();

        if (existingMaps.Count > 0)
        {
            foreach (var existingMap in existingMaps)
            {
                if (ApplyTerminology(existingMap, terminology))
                {
                    existingMap.UpdatedAt = now;
                    existingMap.UpdatedBy = "system";
                    await context.SaveChangesAsync();
                    logger.LogInformation("{BodyCode} structure map terminology updated", source.RegulatoryBodyCode);
                }
            }

            logger.LogInformation("{BodyCode} structure map already exists, skipping", source.RegulatoryBodyCode);
            return;
        }

        if (documents.Count > 1)
        {
            logger.LogError(
                "{BodyCode} structure map not bound: {Count} regulatory documents titled '{Title}' — ambiguous, skipping",
                source.RegulatoryBodyCode, documents.Count, source.DocumentTitle);
            return;
        }

        var document = documents[0];

        var map = new RegulatoryStructureMap
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = document.Id,
            Status = Domain.Enums.RegulatoryStructureMapStatus.Draft,
            PrincipleLabel = terminology.PrincipleLabel,
            StandardLabel = terminology.StandardLabel,
            PersonBlockDescription = terminology.PersonBlockDescription,
            ProviderBlockDescription = terminology.ProviderBlockDescription,
            CreatedAt = now,
            CreatedBy = "system"
        };

        var principleDisplayOrder = 0;
        foreach (var sourcePrinciple in source.Principles)
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
            "Seeded {BodyCode} structure map (Draft): {PrincipleCount} principles, {StandardCount} standards, {FeatureCount} features",
            source.RegulatoryBodyCode,
            map.Principles.Count,
            map.Principles.Sum(p => p.Standards.Count),
            map.Principles.Sum(p => p.Standards.Sum(s => s.Features.Count)));
    }

    /// <summary>Sets the map's labels from the source; true if any value changed. Never touches status or content.</summary>
    private static bool ApplyTerminology(RegulatoryStructureMap map, StructureTerminology terminology)
    {
        var changed = map.PrincipleLabel != terminology.PrincipleLabel
            || map.StandardLabel != terminology.StandardLabel
            || map.PersonBlockDescription != terminology.PersonBlockDescription
            || map.ProviderBlockDescription != terminology.ProviderBlockDescription;

        map.PrincipleLabel = terminology.PrincipleLabel;
        map.StandardLabel = terminology.StandardLabel;
        map.PersonBlockDescription = terminology.PersonBlockDescription;
        map.ProviderBlockDescription = terminology.ProviderBlockDescription;
        return changed;
    }
}
