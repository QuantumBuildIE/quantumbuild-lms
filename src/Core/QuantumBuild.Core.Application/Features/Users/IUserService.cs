using QuantumBuild.Core.Application.Features.Users.DTOs;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Core.Application.Features.Users;

public interface IUserService
{
    Task<Result<List<UserDto>>> GetAllAsync();
    Task<Result<PaginatedList<UserDto>>> GetPaginatedAsync(GetUsersQueryDto query);
    Task<Result<UserDto>> GetByIdAsync(Guid id);
    Task<Result<UserDto>> CreateAsync(CreateUserDto dto);
    Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserDto dto);
    Task<Result> DeleteAsync(Guid id);
    Task<Result> ResetPasswordAsync(Guid id, ResetPasswordDto dto);
    Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);

    /// <summary>
    /// Sets a user's password using an email verification token.
    /// Used when new users click the "Set Password" link in their welcome email.
    /// </summary>
    Task<Result> SetPasswordWithTokenAsync(SetPasswordWithTokenDto dto);

    /// <summary>
    /// Gets users that do not have a linked employee record
    /// </summary>
    Task<Result<List<UnlinkedUserDto>>> GetUnlinkedAsync();

    /// <summary>
    /// Links an existing user to an existing employee record
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="dto">DTO containing the employee ID to link</param>
    /// <returns>Updated user DTO</returns>
    Task<Result<UserDto>> LinkToEmployeeAsync(Guid userId, LinkUserToEmployeeDto dto);

    /// <summary>
    /// Creates a new employee record for an existing user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="dto">DTO containing the employee details</param>
    /// <returns>Updated user DTO</returns>
    Task<Result<UserDto>> CreateEmployeeForUserAsync(Guid userId, CreateEmployeeForUserDto dto);

    /// <summary>
    /// Unlinks the employee record from a user (does not delete the employee)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> UnlinkEmployeeAsync(Guid userId);
}
