namespace QuantumBuild.Core.Application.Features.Users.DTOs;

public enum EmployeeLinkOption
{
    None = 0,
    LinkExisting = 1,
    CreateNew = 2
}

public record CreateUserDto(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    string ConfirmPassword,
    bool IsActive,
    List<Guid> RoleIds,
    EmployeeLinkOption EmployeeLinkOption = EmployeeLinkOption.None,
    Guid? ExistingEmployeeId = null,
    CreateUserEmployeeDto? NewEmployee = null
);
