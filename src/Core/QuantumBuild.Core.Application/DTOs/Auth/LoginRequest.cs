namespace QuantumBuild.Core.Application.DTOs.Auth;

/// <summary>
/// Request DTO for user login
/// </summary>
public record LoginRequest(
    string Email,
    string Password
);
