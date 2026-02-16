using Microsoft.AspNetCore.Identity;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Application role entity extending ASP.NET Identity
/// </summary>
public class Role : IdentityRole<Guid>
{
    /// <summary>
    /// Tenant this role belongs to (null for system-wide roles)
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Description of the role
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this is a system role (cannot be deleted)
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>
    /// Whether the role is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date/time role was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Who created this role
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date/time role was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Who last updated this role
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Navigation property to role permissions
    /// </summary>
    public virtual ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    /// <summary>
    /// Navigation property to user roles
    /// </summary>
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
