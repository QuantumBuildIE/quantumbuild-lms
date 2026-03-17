using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Sectors;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Sectors;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Sectors;

public class SectorService(IToolboxTalksDbContext dbContext) : ISectorService
{
    public async Task<List<SectorDto>> GetActiveSectorsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Sectors
            .IgnoreQueryFilters()
            .Where(s => s.IsActive && !s.IsDeleted)
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new SectorDto
            {
                Id = s.Id,
                Key = s.Key,
                Name = s.Name,
                Icon = s.Icon,
                DisplayOrder = s.DisplayOrder
            })
            .ToListAsync(cancellationToken);
    }
}
