namespace QuantumBuild.Core.Application.Features.Employees.DTOs;

/// <summary>
/// DTO for linking an existing employee to an existing user
/// </summary>
public record LinkEmployeeToUserDto(
    Guid UserId
);

/// <summary>
/// DTO for creating a new user account for an existing employee
/// </summary>
public record CreateUserForEmployeeDto(
    List<Guid> RoleIds
);
