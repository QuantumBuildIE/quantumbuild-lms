using QuantumBuild.Core.Application.Features.Employees.DTOs;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.Employees;

public interface IEmployeeService
{
    Task<Result<List<EmployeeDto>>> GetAllAsync();
    Task<Result<PaginatedList<EmployeeDto>>> GetPaginatedAsync(GetEmployeesQueryDto query);
    Task<Result<EmployeeDto>> GetByIdAsync(Guid id);
    Task<Result<EmployeeDto>> CreateAsync(CreateEmployeeDto dto);
    Task<Result<EmployeeDto>> UpdateAsync(Guid id, UpdateEmployeeDto dto);
    Task<Result> DeleteAsync(Guid id);
    Task<Result<List<EmployeeDto>>> GetUnlinkedAsync();

    /// <summary>
    /// Resends the welcome/password setup email to an employee with a linked user account
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> ResendInviteAsync(Guid employeeId);

    /// <summary>
    /// Links an existing employee to an existing user account
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <param name="dto">DTO containing the user ID to link</param>
    /// <returns>Updated employee DTO</returns>
    Task<Result<EmployeeDto>> LinkToUserAsync(Guid employeeId, LinkEmployeeToUserDto dto);

    /// <summary>
    /// Creates a new user account for an existing employee
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <param name="dto">DTO containing the role IDs for the new user</param>
    /// <returns>Updated employee DTO</returns>
    Task<Result<EmployeeDto>> CreateUserForEmployeeAsync(Guid employeeId, CreateUserForEmployeeDto dto);

    /// <summary>
    /// Unlinks the user account from an employee (does not delete the user)
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> UnlinkUserAsync(Guid employeeId);
}
