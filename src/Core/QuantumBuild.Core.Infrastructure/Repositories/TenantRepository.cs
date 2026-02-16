using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for tenant operations
/// </summary>
public class TenantRepository : ITenantRepository
{
    private readonly ICoreDbContext _context;

    public TenantRepository(ICoreDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Tenant>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted && t.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, cancellationToken);
    }
}
