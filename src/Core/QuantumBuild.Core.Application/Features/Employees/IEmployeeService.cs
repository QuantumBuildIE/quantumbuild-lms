using QuantumBuild.Core.Application.Features.Employees.DTOs;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.Employees;

public interface IEmployeeService
{
    Task<Result<List<EmployeeDto>>> GetAllAsync();
    Task<Result<PaginatedList<EmployeeDto>>> GetPaginatedAsync(GetEmployeesQueryDto query);
    Task<Result<EmployeeDto>> GetByIdAsync(Guid id);
    /// <param name="sendInvitationEmail">
    /// When false, skips sending the password-setup invitation email. Pass false from
    /// background jobs that manage email delivery separately (e.g. bulk import).
    /// </param>
    /// <param name="tenantIdOverride">
    /// When provided, overrides ICurrentUserService.TenantId. Required when called from
    /// a Hangfire job that has no HTTP context (and therefore no JWT-derived tenant).
    /// </param>
    Task<Result<EmployeeDto>> CreateAsync(
        CreateEmployeeDto dto,
        bool sendInvitationEmail = true,
        Guid? tenantIdOverride = null);
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

    /// <summary>
    /// Resets the employee's QR workstation PIN and emails the new PIN to them.
    /// Returns a failure result if QR Location Training is not enabled for the tenant.
    /// </summary>
    Task<Result> ResetPinAsync(Guid employeeId, CancellationToken ct = default);

    /// <summary>
    /// Generates the next available employee code for the tenant (e.g. EMP001, EMP002).
    /// Skips soft-deleted codes to avoid reuse.
    /// </summary>
    Task<string> GenerateEmployeeCodeAsync(Guid tenantId);
}
