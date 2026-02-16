namespace QuantumBuild.Core.Application.DTOs.Auth;

/// <summary>
/// Request DTO for refreshing JWT token
/// </summary>
public record RefreshTokenRequest(
    string AccessToken,
    string RefreshToken
);
