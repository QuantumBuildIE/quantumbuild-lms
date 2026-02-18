namespace QuantumBuild.Core.Application.Interfaces;

/// <summary>
/// Interface for accessing current user information
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Current user's ID
    /// </summary>
    string UserId { get; }

    /// <summary>
    /// Current user's name
    /// </summary>
    string UserName { get; }

    /// <summary>
    /// Current user's tenant ID.
    /// For SuperUser, reads from X-Tenant-Id header; returns Guid.Empty if no header (bypass tenant filter).
    /// </summary>
    Guid TenantId { get; }

    /// <summary>
    /// Whether the current user is a super user
    /// </summary>
    bool IsSuperUser { get; }
}
