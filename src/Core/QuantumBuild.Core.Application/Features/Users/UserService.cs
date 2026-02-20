using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Features.Users.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.Users;

public class UserService : IUserService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICoreDbContext _context;

    public UserService(
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ICurrentUserService currentUserService,
        ICoreDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUserService = currentUserService;
        _context = context;
    }

    public async Task<Result<List<UserDto>>> GetAllAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var users = await _userManager.Users
                .Where(u => u.TenantId == tenantId)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Employee)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Select(u => new UserDto(
                    u.Id,
                    u.Email!,
                    u.FirstName,
                    u.LastName,
                    u.FirstName + " " + u.LastName,
                    u.TenantId,
                    u.IsActive,
                    u.UserRoles.Select(ur => new UserRoleDto(ur.RoleId, ur.Role.Name!)).ToList(),
                    u.CreatedAt,
                    u.EmployeeId,
                    u.Employee != null ? u.Employee.FirstName + " " + u.Employee.LastName : null
                ))
                .ToListAsync();

            return Result.Ok(users);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<UserDto>>($"Error retrieving users: {ex.Message}");
        }
    }

    public async Task<Result<PaginatedList<UserDto>>> GetPaginatedAsync(GetUsersQueryDto query)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var usersQuery = _userManager.Users
                .Where(u => u.TenantId == tenantId)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Employee)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLower = query.Search.ToLower();
                usersQuery = usersQuery.Where(u =>
                    u.Email!.ToLower().Contains(searchLower) ||
                    u.FirstName.ToLower().Contains(searchLower) ||
                    u.LastName.ToLower().Contains(searchLower) ||
                    (u.FirstName + " " + u.LastName).ToLower().Contains(searchLower)
                );
            }

            // Apply sorting
            usersQuery = ApplySorting(usersQuery, query.SortColumn, query.SortDirection);

            // Get total count before pagination
            var totalCount = await usersQuery.CountAsync();

            // Apply pagination
            var users = await usersQuery
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(u => new UserDto(
                    u.Id,
                    u.Email!,
                    u.FirstName,
                    u.LastName,
                    u.FirstName + " " + u.LastName,
                    u.TenantId,
                    u.IsActive,
                    u.UserRoles.Select(ur => new UserRoleDto(ur.RoleId, ur.Role.Name!)).ToList(),
                    u.CreatedAt,
                    u.EmployeeId,
                    u.Employee != null ? u.Employee.FirstName + " " + u.Employee.LastName : null
                ))
                .ToListAsync();

            var result = new PaginatedList<UserDto>(users, totalCount, query.PageNumber, query.PageSize);
            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            return Result.Fail<PaginatedList<UserDto>>($"Error retrieving users: {ex.Message}");
        }
    }

    private static IQueryable<User> ApplySorting(IQueryable<User> query, string? sortColumn, string? sortDirection)
    {
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortColumn?.ToLower() switch
        {
            "email" => isDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "firstname" => isDescending ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName),
            "lastname" => isDescending ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName),
            "fullname" or "name" => isDescending
                ? query.OrderByDescending(u => u.FirstName + " " + u.LastName)
                : query.OrderBy(u => u.FirstName + " " + u.LastName),
            "isactive" => isDescending ? query.OrderByDescending(u => u.IsActive) : query.OrderBy(u => u.IsActive),
            "createdat" => isDescending ? query.OrderByDescending(u => u.CreatedAt) : query.OrderBy(u => u.CreatedAt),
            _ => query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
        };
    }

    public async Task<Result<UserDto>> GetByIdAsync(Guid id)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var user = await _userManager.Users
                .Where(u => u.Id == id && u.TenantId == tenantId)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Employee)
                .Select(u => new UserDto(
                    u.Id,
                    u.Email!,
                    u.FirstName,
                    u.LastName,
                    u.FirstName + " " + u.LastName,
                    u.TenantId,
                    u.IsActive,
                    u.UserRoles.Select(ur => new UserRoleDto(ur.RoleId, ur.Role.Name!)).ToList(),
                    u.CreatedAt,
                    u.EmployeeId,
                    u.Employee != null ? u.Employee.FirstName + " " + u.Employee.LastName : null
                ))
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Result.Fail<UserDto>($"User with ID {id} not found");
            }

            return Result.Ok(user);
        }
        catch (Exception ex)
        {
            return Result.Fail<UserDto>($"Error retrieving user: {ex.Message}");
        }
    }

    public async Task<Result<UserDto>> CreateAsync(CreateUserDto dto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            // Check for existing email within the same tenant
            var existingUser = await _userManager.Users
                .Where(u => u.TenantId == tenantId && u.NormalizedEmail == dto.Email.ToUpperInvariant())
                .FirstOrDefaultAsync();
            if (existingUser != null)
            {
                return Result.Fail<UserDto>($"A user with email '{dto.Email}' already exists");
            }

            // Validate roles exist
            var roles = await _roleManager.Roles
                .Where(r => dto.RoleIds.Contains(r.Id))
                .ToListAsync();

            if (roles.Count != dto.RoleIds.Count)
            {
                return Result.Fail<UserDto>("One or more role IDs are invalid");
            }

            var user = new User
            {
                UserName = dto.Email,
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                TenantId = tenantId,
                IsActive = dto.IsActive,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUserService.UserId
            };

            var createResult = await _userManager.CreateAsync(user, dto.Password);
            if (!createResult.Succeeded)
            {
                var errors = createResult.Errors.Select(e => e.Description).ToList();
                return Result.Fail<UserDto>(errors);
            }

            // Add user to roles
            foreach (var role in roles)
            {
                await _userManager.AddToRoleAsync(user, role.Name!);
            }

            // Handle employee linking
            switch (dto.EmployeeLinkOption)
            {
                case EmployeeLinkOption.LinkExisting:
                {
                    if (!dto.ExistingEmployeeId.HasValue)
                    {
                        return Result.Fail<UserDto>("ExistingEmployeeId is required when linking to an existing employee");
                    }

                    var employee = await _context.Employees
                        .FirstOrDefaultAsync(e => e.Id == dto.ExistingEmployeeId.Value);

                    if (employee == null)
                    {
                        return Result.Fail<UserDto>($"Employee with ID {dto.ExistingEmployeeId} not found");
                    }

                    if (employee.UserId.HasValue)
                    {
                        return Result.Fail<UserDto>("This employee is already linked to a user account");
                    }

                    employee.UserId = user.Id;
                    user.EmployeeId = employee.Id;

                    await _userManager.UpdateAsync(user);
                    await _context.SaveChangesAsync();
                    break;
                }
                case EmployeeLinkOption.CreateNew:
                {
                    if (dto.NewEmployee == null)
                    {
                        return Result.Fail<UserDto>("NewEmployee data is required when creating a new employee");
                    }

                    // Validate employee code uniqueness within tenant
                    var duplicateCode = await _context.Employees
                        .AnyAsync(e => e.EmployeeCode == dto.NewEmployee.EmployeeCode);
                    if (duplicateCode)
                    {
                        return Result.Fail<UserDto>($"Employee with code '{dto.NewEmployee.EmployeeCode}' already exists");
                    }

                    // Validate PrimarySiteId if provided
                    if (dto.NewEmployee.PrimarySiteId.HasValue)
                    {
                        var siteExists = await _context.Sites
                            .AnyAsync(s => s.Id == dto.NewEmployee.PrimarySiteId.Value);
                        if (!siteExists)
                        {
                            return Result.Fail<UserDto>($"Site with ID {dto.NewEmployee.PrimarySiteId} not found");
                        }
                    }

                    var newEmployee = new Employee
                    {
                        Id = Guid.NewGuid(),
                        EmployeeCode = dto.NewEmployee.EmployeeCode,
                        FirstName = dto.FirstName,
                        LastName = dto.LastName,
                        Email = dto.Email,
                        Phone = dto.NewEmployee.Phone,
                        Mobile = dto.NewEmployee.Mobile,
                        JobTitle = dto.NewEmployee.JobTitle,
                        Department = dto.NewEmployee.Department,
                        PrimarySiteId = dto.NewEmployee.PrimarySiteId,
                        IsActive = true,
                        UserId = user.Id,
                        TenantId = tenantId
                    };

                    newEmployee.SetGeoTrackerID(dto.NewEmployee.GeoTrackerID);

                    _context.Employees.Add(newEmployee);

                    user.EmployeeId = newEmployee.Id;
                    await _userManager.UpdateAsync(user);
                    await _context.SaveChangesAsync();
                    break;
                }
                case EmployeeLinkOption.None:
                default:
                    break;
            }

            // Reload user with roles and employee
            var createdUser = await _userManager.Users
                .Where(u => u.Id == user.Id)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Employee)
                .FirstAsync();

            var userDto = new UserDto(
                createdUser.Id,
                createdUser.Email!,
                createdUser.FirstName,
                createdUser.LastName,
                createdUser.FirstName + " " + createdUser.LastName,
                createdUser.TenantId,
                createdUser.IsActive,
                createdUser.UserRoles.Select(ur => new UserRoleDto(ur.RoleId, ur.Role.Name!)).ToList(),
                createdUser.CreatedAt,
                createdUser.EmployeeId,
                createdUser.Employee != null
                    ? createdUser.Employee.FirstName + " " + createdUser.Employee.LastName
                    : null
            );

            return Result.Ok(userDto);
        }
        catch (Exception ex)
        {
            return Result.Fail<UserDto>($"Error creating user: {ex.Message}");
        }
    }

    public async Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserDto dto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var user = await _userManager.Users
                .Where(u => u.Id == id && u.TenantId == tenantId)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Result.Fail<UserDto>($"User with ID {id} not found");
            }

            // Validate roles exist
            var roles = await _roleManager.Roles
                .Where(r => dto.RoleIds.Contains(r.Id))
                .ToListAsync();

            if (roles.Count != dto.RoleIds.Count)
            {
                return Result.Fail<UserDto>("One or more role IDs are invalid");
            }

            // Update user properties
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.IsActive = dto.IsActive;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = _currentUserService.UserId;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = updateResult.Errors.Select(e => e.Description).ToList();
                return Result.Fail<UserDto>(errors);
            }

            // Update roles - remove current roles and add new ones
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }

            foreach (var role in roles)
            {
                await _userManager.AddToRoleAsync(user, role.Name!);
            }

            // Reload user with roles and employee
            var updatedUser = await _userManager.Users
                .Where(u => u.Id == user.Id)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(u => u.Employee)
                .FirstAsync();

            var userDto = new UserDto(
                updatedUser.Id,
                updatedUser.Email!,
                updatedUser.FirstName,
                updatedUser.LastName,
                updatedUser.FirstName + " " + updatedUser.LastName,
                updatedUser.TenantId,
                updatedUser.IsActive,
                updatedUser.UserRoles.Select(ur => new UserRoleDto(ur.RoleId, ur.Role.Name!)).ToList(),
                updatedUser.CreatedAt,
                updatedUser.EmployeeId,
                updatedUser.Employee != null
                    ? updatedUser.Employee.FirstName + " " + updatedUser.Employee.LastName
                    : null
            );

            return Result.Ok(userDto);
        }
        catch (Exception ex)
        {
            return Result.Fail<UserDto>($"Error updating user: {ex.Message}");
        }
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var user = await _userManager.Users
                .Where(u => u.Id == id && u.TenantId == tenantId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Result.Fail($"User with ID {id} not found");
            }

            // Soft delete by deactivating the user
            user.IsActive = false;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = _currentUserService.UserId;

            await _userManager.UpdateAsync(user);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Error deleting user: {ex.Message}");
        }
    }

    public async Task<Result> ResetPasswordAsync(Guid id, ResetPasswordDto dto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var user = await _userManager.Users
                .Where(u => u.Id == id && u.TenantId == tenantId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Result.Fail($"User with ID {id} not found");
            }

            // Generate password reset token and reset password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return Result.Fail(errors);
            }

            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = _currentUserService.UserId;
            await _userManager.UpdateAsync(user);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Error resetting password: {ex.Message}");
        }
    }

    public async Task<Result> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                return Result.Fail("User not found");
            }

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();
                return Result.Fail(errors);
            }

            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = _currentUserService.UserId;
            await _userManager.UpdateAsync(user);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Error changing password: {ex.Message}");
        }
    }

    public async Task<Result> SetPasswordWithTokenAsync(SetPasswordWithTokenDto dto)
    {
        try
        {
            // Validate passwords match
            if (dto.NewPassword != dto.ConfirmPassword)
            {
                return Result.Fail("Passwords do not match");
            }

            // Find user by email
            var user = await _userManager.FindByEmailAsync(dto.Email);

            if (user == null)
            {
                // Return generic error to avoid user enumeration
                return Result.Fail("Invalid token or email");
            }

            // Validate and reset password using the token
            var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description).ToList();

                // Check for specific token errors and return generic message
                if (errors.Any(e => e.Contains("Invalid token", StringComparison.OrdinalIgnoreCase)))
                {
                    return Result.Fail("This password setup link has expired or is invalid. Please contact your administrator.");
                }

                return Result.Fail(errors);
            }

            // Mark email as confirmed since user has verified ownership
            user.EmailConfirmed = true;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Error setting password: {ex.Message}");
        }
    }

    public async Task<Result<List<UnlinkedUserDto>>> GetUnlinkedAsync()
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var users = await _userManager.Users
                .Where(u => u.TenantId == tenantId && u.EmployeeId == null && u.IsActive)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .OrderBy(u => u.LastName)
                .ThenBy(u => u.FirstName)
                .Select(u => new UnlinkedUserDto(
                    u.Id,
                    u.Email!,
                    u.FirstName,
                    u.LastName,
                    u.FirstName + " " + u.LastName,
                    u.IsActive,
                    u.UserRoles.Select(ur => ur.Role.Name!).ToList()
                ))
                .ToListAsync();

            return Result.Ok(users);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<UnlinkedUserDto>>($"Error retrieving unlinked users: {ex.Message}");
        }
    }

    public async Task<Result<UserDto>> LinkToEmployeeAsync(Guid userId, LinkUserToEmployeeDto dto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var user = await _userManager.Users
                .Where(u => u.Id == userId && u.TenantId == tenantId)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Result.Fail<UserDto>($"User with ID {userId} not found");
            }

            if (user.EmployeeId.HasValue)
            {
                return Result.Fail<UserDto>("This user is already linked to an employee");
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == dto.EmployeeId);

            if (employee == null)
            {
                return Result.Fail<UserDto>($"Employee with ID {dto.EmployeeId} not found");
            }

            if (employee.UserId.HasValue)
            {
                return Result.Fail<UserDto>("This employee is already linked to another user account");
            }

            // Create the bidirectional link
            user.EmployeeId = employee.Id;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = _currentUserService.UserId;
            employee.UserId = user.Id;

            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();

            var userDto = new UserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                user.FirstName + " " + user.LastName,
                user.TenantId,
                user.IsActive,
                user.UserRoles.Select(ur => new UserRoleDto(ur.RoleId, ur.Role.Name!)).ToList(),
                user.CreatedAt,
                employee.Id,
                employee.FirstName + " " + employee.LastName
            );

            return Result.Ok(userDto);
        }
        catch (Exception ex)
        {
            return Result.Fail<UserDto>($"Error linking user to employee: {ex.Message}");
        }
    }

    public async Task<Result<UserDto>> CreateEmployeeForUserAsync(Guid userId, CreateEmployeeForUserDto dto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var user = await _userManager.Users
                .Where(u => u.Id == userId && u.TenantId == tenantId)
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Result.Fail<UserDto>($"User with ID {userId} not found");
            }

            if (user.EmployeeId.HasValue)
            {
                return Result.Fail<UserDto>("This user is already linked to an employee");
            }

            // Check employee code uniqueness (including soft-deleted records)
            var duplicateCode = await _context.Employees
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId && e.EmployeeCode == dto.EmployeeCode)
                .Select(e => new { e.Id, e.FirstName, e.LastName, e.IsDeleted })
                .FirstOrDefaultAsync();

            if (duplicateCode != null)
            {
                if (duplicateCode.IsDeleted)
                {
                    return Result.Fail<UserDto>(
                        $"Employee code '{dto.EmployeeCode}' was previously assigned to deleted employee " +
                        $"{duplicateCode.FirstName} {duplicateCode.LastName}. " +
                        "Please choose a different code.");
                }
                return Result.Fail<UserDto>($"Employee code '{dto.EmployeeCode}' is already in use.");
            }

            // Validate site if provided
            if (dto.PrimarySiteId.HasValue)
            {
                var siteExists = await _context.Sites.AnyAsync(s => s.Id == dto.PrimarySiteId.Value);
                if (!siteExists)
                {
                    return Result.Fail<UserDto>($"Site with ID {dto.PrimarySiteId} not found");
                }
            }

            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeCode = dto.EmployeeCode,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                JobTitle = dto.JobTitle,
                Department = dto.Department,
                PrimarySiteId = dto.PrimarySiteId,
                IsActive = true,
                UserId = user.Id,
                TenantId = tenantId,
                PreferredLanguage = dto.PreferredLanguage
            };

            employee.SetGeoTrackerID(dto.GeoTrackerID);

            _context.Employees.Add(employee);

            user.EmployeeId = employee.Id;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = _currentUserService.UserId;

            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();

            var userDto = new UserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                user.FirstName + " " + user.LastName,
                user.TenantId,
                user.IsActive,
                user.UserRoles.Select(ur => new UserRoleDto(ur.RoleId, ur.Role.Name!)).ToList(),
                user.CreatedAt,
                employee.Id,
                employee.FirstName + " " + employee.LastName
            );

            return Result.Ok(userDto);
        }
        catch (Exception ex)
        {
            return Result.Fail<UserDto>($"Error creating employee: {ex.Message}");
        }
    }

    public async Task<Result> UnlinkEmployeeAsync(Guid userId)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            var user = await _userManager.Users
                .Where(u => u.Id == userId && u.TenantId == tenantId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Result.Fail($"User with ID {userId} not found");
            }

            if (!user.EmployeeId.HasValue)
            {
                return Result.Fail("This user is not linked to any employee");
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value);

            if (employee != null)
            {
                employee.UserId = null;
            }

            var previousEmployeeId = user.EmployeeId;
            user.EmployeeId = null;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = _currentUserService.UserId;

            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();

            return Result.Ok();
        }
        catch (Exception ex)
        {
            return Result.Fail($"Error unlinking employee: {ex.Message}");
        }
    }
}
