using QuantumBuild.Core.Domain.Common;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Permission entity representing a specific action that can be granted to roles
/// </summary>
public class Permission : BaseEntity
{
    /// <summary>
    /// Unique permission name (e.g., "StockManagement.Products.Create")
    /// Format: {Module}.{Entity}.{Action}
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the permission
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Module this permission belongs to (e.g., "StockManagement", "SiteAttendance")
    /// </summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property to role permissions
    /// </summary>
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
