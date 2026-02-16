namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Join entity between Role and Permission for many-to-many relationship
/// </summary>
public class RolePermission
{
    /// <summary>
    /// Foreign key to Role
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Navigation property to Role
    /// </summary>
    public virtual Role Role { get; set; } = null!;

    /// <summary>
    /// Foreign key to Permission
    /// </summary>
    public Guid PermissionId { get; set; }

    /// <summary>
    /// Navigation property to Permission
    /// </summary>
    public virtual Permission Permission { get; set; } = null!;
}
