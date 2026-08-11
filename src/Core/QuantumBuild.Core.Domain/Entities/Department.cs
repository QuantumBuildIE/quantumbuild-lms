using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Tenant-scoped department lookup for organising employees.
/// Structurally mirrors Site (tenant-scoped, soft-deletable, optional single FK on Employee)
/// but without Site's construction/geofencing-specific attributes.
/// </summary>
public class Department : TenantEntity
{
    /// <summary>
    /// Department display name. Unique per tenant.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional short code for the department.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Department status - true if active. Inactive departments are hidden from
    /// selection but remain resolvable for employees already assigned to them.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
