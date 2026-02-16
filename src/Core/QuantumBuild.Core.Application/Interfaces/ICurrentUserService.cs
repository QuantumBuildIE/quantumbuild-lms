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
    /// Current user's tenant ID
    /// </summary>
    Guid TenantId { get; }
}
