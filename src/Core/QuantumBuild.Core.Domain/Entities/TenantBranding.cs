using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Per-tenant branding (currently the logo only). One row per tenant.
/// Deliberately a BaseEntity rather than a TenantEntity: there is no tenant query filter, so a
/// SuperUser acting on behalf of tenant Y while X-Tenant-Id points at X still sees Y's row.
/// Every query must filter on TenantId explicitly. Rows are never soft-deleted by the logo feature;
/// removing a logo nulls LogoKey.
/// </summary>
public class TenantBranding : BaseEntity
{
    /// <summary>
    /// Tenant this branding belongs to (unique)
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// R2 object key of the current logo ({tenantId}/branding/logo-{guid}.{ext}); null when no logo is set
    /// </summary>
    public string? LogoKey { get; set; }

    // Navigation properties
    public Tenant Tenant { get; set; } = null!;
}
