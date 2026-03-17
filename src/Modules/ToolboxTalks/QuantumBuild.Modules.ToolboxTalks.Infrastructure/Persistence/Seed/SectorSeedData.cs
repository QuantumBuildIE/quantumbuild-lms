using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds default Sector records matching existing SectorKey string values.
/// </summary>
public static class SectorSeedData
{
    public static async Task SeedAsync(DbContext context, ILogger logger)
    {
        var existingKeys = await context.Set<Sector>()
            .IgnoreQueryFilters()
            .Select(s => s.Key)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var sectors = new List<Sector>();

        var seedSectors = new (string Key, string Name, string Icon, int DisplayOrder)[]
        {
            ("construction", "Construction", "\U0001F3D7\uFE0F", 1),
            ("homecare", "Homecare", "\U0001F3E0", 2),
            ("manufacturing", "Manufacturing", "\U0001F3ED", 3),
            ("transport", "Transport", "\U0001F69A", 4),
            ("food_hospitality", "Food & Hospitality", "\U0001F37D\uFE0F", 5),
        };

        foreach (var (key, name, icon, displayOrder) in seedSectors)
        {
            if (existingKeys.Contains(key))
                continue;

            sectors.Add(new Sector
            {
                Id = Guid.NewGuid(),
                Key = key,
                Name = name,
                Icon = icon,
                DisplayOrder = displayOrder,
                IsActive = true,
                CreatedAt = now,
                CreatedBy = "system"
            });
        }

        if (sectors.Count == 0)
        {
            logger.LogInformation("All sectors already exist, skipping");
            return;
        }

        await context.Set<Sector>().AddRangeAsync(sectors);
        await context.SaveChangesAsync();

        logger.LogInformation("Seeded {SectorCount} sectors", sectors.Count);
    }
}
