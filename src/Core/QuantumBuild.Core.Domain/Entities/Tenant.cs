using QuantumBuild.Core.Domain.Common;

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
}
