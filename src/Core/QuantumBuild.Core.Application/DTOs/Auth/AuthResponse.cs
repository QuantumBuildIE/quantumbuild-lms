namespace QuantumBuild.Core.Application.DTOs.Auth;

/// <summary>
/// Response DTO for authentication operations
/// </summary>
public record AuthResponse(
    bool Success,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    UserInfo? User,
    IEnumerable<string>? Errors
)
{
    public static AuthResponse Failure(IEnumerable<string> errors)
        => new(false, null, null, null, null, errors);

    public static AuthResponse Failure(string error)
        => new(false, null, null, null, null, new[] { error });
}

/// <summary>
/// User information returned in auth responses
/// </summary>
public record UserInfo(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    Guid TenantId,
    IEnumerable<string> Roles,
    IEnumerable<string> Permissions,
    bool IsSuperUser,
    Guid? EmployeeId
);
