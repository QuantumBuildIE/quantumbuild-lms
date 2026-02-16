namespace QuantumBuild.Core.Application.Features.Users.DTOs;

public record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    Guid TenantId,
    bool IsActive,
    List<UserRoleDto> Roles,
    DateTime CreatedAt,
    Guid? EmployeeId = null,
    string? EmployeeName = null
);

public record UserRoleDto(
    Guid Id,
    string Name
);
