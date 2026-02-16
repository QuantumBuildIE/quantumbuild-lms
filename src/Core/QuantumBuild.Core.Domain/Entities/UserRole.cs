using Microsoft.AspNetCore.Identity;

namespace QuantumBuild.Core.Domain.Entities;

/// <summary>
/// Join entity between User and Role with additional navigation properties
/// </summary>
public class UserRole : IdentityUserRole<Guid>
{
    /// <summary>
    /// Navigation property to User
    /// </summary>
    public virtual User User { get; set; } = null!;

    /// <summary>
    /// Navigation property to Role
    /// </summary>
    public virtual Role Role { get; set; } = null!;
}
