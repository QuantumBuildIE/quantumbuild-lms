using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Interfaces;

/// <summary>
/// Repository interface for tenant operations
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// Gets all active tenants
    /// </summary>
    Task<IEnumerable<Tenant>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a tenant by ID
    /// </summary>
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
