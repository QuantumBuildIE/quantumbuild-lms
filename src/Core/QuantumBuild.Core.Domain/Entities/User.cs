using Microsoft.AspNetCore.Identity;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Application user entity extending ASP.NET Identity
/// </summary>
public class User : IdentityUser<Guid>
{
    /// <summary>
    /// Tenant this user belongs to
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// User's first name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Full name computed from first and last name
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Whether the user account is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether the user is a super user with cross-tenant access
    /// </summary>
    public bool IsSuperUser { get; set; }

    /// <summary>
    /// Optional link to Employee record
    /// </summary>
    public Guid? EmployeeId { get; set; }

    /// <summary>
    /// Navigation property to Employee
    /// </summary>
    public virtual Employee? Employee { get; set; }

    /// <summary>
    /// Refresh token for JWT authentication
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Refresh token expiry time
    /// </summary>
    public DateTime? RefreshTokenExpiryTime { get; set; }

    /// <summary>
    /// Date/time user was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Who created this user
    /// </summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>
    /// Date/time user was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Who last updated this user
    /// </summary>
    public string? UpdatedBy { get; set; }

    /// <summary>
    /// Navigation property to user roles
    /// </summary>
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
