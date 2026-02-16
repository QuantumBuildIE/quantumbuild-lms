using QuantumBuild.Core.Application.DTOs.Auth;

namespace QuantumBuild.Core.Application.Interfaces;

/// <summary>
/// Service interface for authentication operations
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticate a user with email and password
    /// </summary>
    Task<AuthResponse> LoginAsync(LoginRequest request);

    /// <summary>
    /// Register a new user
    /// </summary>
    Task<AuthResponse> RegisterAsync(RegisterRequest request);

    /// <summary>
    /// Refresh an expired JWT token using a refresh token
    /// </summary>
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);

    /// <summary>
    /// Revoke a user's refresh token (logout)
    /// </summary>
    Task<bool> RevokeTokenAsync(Guid userId);

    /// <summary>
    /// Get all permissions for a user (through their roles)
    /// </summary>
    Task<IEnumerable<string>> GetUserPermissionsAsync(Guid userId);
}
