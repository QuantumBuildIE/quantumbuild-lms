namespace QuantumBuild.Core.Application.Features.Users.DTOs;

/// <summary>
/// DTO for linking an existing user to an existing employee
/// </summary>
public record LinkUserToEmployeeDto(
    Guid EmployeeId
);

/// <summary>
/// DTO for creating a new employee for an existing user
/// </summary>
public record CreateEmployeeForUserDto(
    string EmployeeCode,
    string? Phone = null,
    string? Mobile = null,
    string? JobTitle = null,
    string? Department = null,
    Guid? PrimarySiteId = null,
    string? GeoTrackerID = null,
    string PreferredLanguage = "en"
);

/// <summary>
/// Lightweight user info for unlinked users list
/// </summary>
public record UnlinkedUserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    bool IsActive,
    List<string> RoleNames
);
