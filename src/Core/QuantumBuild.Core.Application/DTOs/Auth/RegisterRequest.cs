namespace QuantumBuild.Core.Application.DTOs.Auth;

/// <summary>
/// Request DTO for user registration
/// </summary>
public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    Guid TenantId
);
