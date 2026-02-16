namespace QuantumBuild.Core.Domain.Common;

/// <summary>
/// Base entity for multi-tenant entities
/// Inherits audit fields and soft delete from BaseEntity and adds TenantId
/// </summary>
public abstract class TenantEntity : BaseEntity
{
    /// <summary>
    /// Tenant identifier for multi-tenancy isolation
    /// All queries should be automatically filtered by this field
    /// </summary>
    public Guid TenantId { get; set; }
}
