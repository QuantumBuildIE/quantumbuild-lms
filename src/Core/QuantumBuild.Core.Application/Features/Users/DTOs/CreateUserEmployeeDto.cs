namespace QuantumBuild.Core.Application.Features.Users.DTOs;

/// <summary>
/// DTO for creating a new employee inline when creating a user.
/// FirstName, LastName, and Email are copied from the User being created.
/// </summary>
public record CreateUserEmployeeDto(
    string EmployeeCode,
    string? Phone,
    string? Mobile,
    string? JobTitle,
    string? Department,
    Guid? PrimarySiteId,
    string? GeoTrackerID = null
);
