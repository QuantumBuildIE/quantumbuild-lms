using QuantumBuild.Core.Domain.Common;
using QuantumBuild.Core.Domain.Enums;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Tenant entity representing a company/organization using the system
/// </summary>
public class Tenant : BaseEntity
{
    /// <summary>
    /// Name of the tenant/organization
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier/code for the tenant
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Whether the tenant account is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Company/organization name associated with the tenant
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Current status of the tenant
    /// </summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>
    /// Primary contact email for the tenant
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Primary contact name for the tenant
    /// </summary>
    public string? ContactName { get; set; }
}
