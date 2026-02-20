using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Features.Employees.DTOs;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.Employees;

public class EmployeeService : IEmployeeService
{
    private readonly ICoreDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;
    private readonly ICurrentUserService _currentUserService;
    private readonly IEmailService _emailService;
    private readonly ILogger<EmployeeService> _logger;
    private readonly IEnumerable<INewEmployeeTrainingAssigner> _trainingAssigners;

    private const string DefaultUserRole = "Operator";

    public EmployeeService(
        ICoreDbContext context,
        UserManager<User> userManager,
        RoleManager<Role> roleManager,
        ICurrentUserService currentUserService,
        IEmailService emailService,
        ILogger<EmployeeService> logger,
        IEnumerable<INewEmployeeTrainingAssigner> trainingAssigners)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _currentUserService = currentUserService;
        _emailService = emailService;
        _logger = logger;
        _trainingAssigners = trainingAssigners;
    }

    public async Task<Result<List<EmployeeDto>>> GetAllAsync()
    {
        try
        {
            var employees = await _context.Employees
                .Include(e => e.PrimarySite)
                .OrderBy(e => e.EmployeeCode)
                .Select(e => new EmployeeDto(
                    e.Id,
                    e.EmployeeCode,
                    e.FirstName,
                    e.LastName,
                    e.FirstName + " " + e.LastName,
                    e.Email,
                    e.Phone,
                    e.Mobile,
                    e.JobTitle,
                    e.Department,
                    e.PrimarySiteId,
                    e.PrimarySite != null ? e.PrimarySite.SiteName : null,
                    e.StartDate,
                    e.EndDate,
                    e.IsActive,
                    e.Notes,
                    e.GeoTrackerID,
                    e.UserId != null,
                    e.UserId,
                    e.PreferredLanguage,
                    e.FloatPersonId,
                    e.FloatLinkedAt,
                    e.FloatLinkMethod
                ))
                .ToListAsync();

            return Result.Ok(employees);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<EmployeeDto>>($"Error retrieving employees: {ex.Message}");
        }
    }

    public async Task<Result<PaginatedList<EmployeeDto>>> GetPaginatedAsync(GetEmployeesQueryDto query)
    {
        try
        {
            var employeesQuery = _context.Employees
                .Include(e => e.PrimarySite)
                .AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLower = query.Search.ToLower();
                employeesQuery = employeesQuery.Where(e =>
                    e.EmployeeCode.ToLower().Contains(searchLower) ||
                    e.FirstName.ToLower().Contains(searchLower) ||
                    e.LastName.ToLower().Contains(searchLower) ||
                    (e.FirstName + " " + e.LastName).ToLower().Contains(searchLower) ||
                    (e.Email != null && e.Email.ToLower().Contains(searchLower)) ||
                    (e.JobTitle != null && e.JobTitle.ToLower().Contains(searchLower))
                );
            }

            // Apply sorting
            employeesQuery = ApplySorting(employeesQuery, query.SortColumn, query.SortDirection);

            // Get total count before pagination
            var totalCount = await employeesQuery.CountAsync();

            // Apply pagination
            var employees = await employeesQuery
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(e => new EmployeeDto(
                    e.Id,
                    e.EmployeeCode,
                    e.FirstName,
                    e.LastName,
                    e.FirstName + " " + e.LastName,
                    e.Email,
                    e.Phone,
                    e.Mobile,
                    e.JobTitle,
                    e.Department,
                    e.PrimarySiteId,
                    e.PrimarySite != null ? e.PrimarySite.SiteName : null,
                    e.StartDate,
                    e.EndDate,
                    e.IsActive,
                    e.Notes,
                    e.GeoTrackerID,
                    e.UserId != null,
                    e.UserId,
                    e.PreferredLanguage,
                    e.FloatPersonId,
                    e.FloatLinkedAt,
                    e.FloatLinkMethod
                ))
                .ToListAsync();

            var result = new PaginatedList<EmployeeDto>(employees, totalCount, query.PageNumber, query.PageSize);
            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            return Result.Fail<PaginatedList<EmployeeDto>>($"Error retrieving employees: {ex.Message}");
        }
    }

    private static IQueryable<Employee> ApplySorting(IQueryable<Employee> query, string? sortColumn, string? sortDirection)
    {
        var isDescending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        return sortColumn?.ToLower() switch
        {
            "employeecode" => isDescending ? query.OrderByDescending(e => e.EmployeeCode) : query.OrderBy(e => e.EmployeeCode),
            "firstname" => isDescending ? query.OrderByDescending(e => e.FirstName) : query.OrderBy(e => e.FirstName),
            "lastname" => isDescending ? query.OrderByDescending(e => e.LastName) : query.OrderBy(e => e.LastName),
            "fullname" or "name" => isDescending
                ? query.OrderByDescending(e => e.FirstName + " " + e.LastName)
                : query.OrderBy(e => e.FirstName + " " + e.LastName),
            "email" => isDescending ? query.OrderByDescending(e => e.Email) : query.OrderBy(e => e.Email),
            "jobtitle" => isDescending ? query.OrderByDescending(e => e.JobTitle) : query.OrderBy(e => e.JobTitle),
            "primarysitename" => isDescending
                ? query.OrderByDescending(e => e.PrimarySite != null ? e.PrimarySite.SiteName : "")
                : query.OrderBy(e => e.PrimarySite != null ? e.PrimarySite.SiteName : ""),
            "isactive" => isDescending ? query.OrderByDescending(e => e.IsActive) : query.OrderBy(e => e.IsActive),
            _ => query.OrderBy(e => e.EmployeeCode)
        };
    }

    public async Task<Result<EmployeeDto>> GetByIdAsync(Guid id)
    {
        try
        {
            var employee = await _context.Employees
                .Include(e => e.PrimarySite)
                .Where(e => e.Id == id)
                .Select(e => new EmployeeDto(
                    e.Id,
                    e.EmployeeCode,
                    e.FirstName,
                    e.LastName,
                    e.FirstName + " " + e.LastName,
                    e.Email,
                    e.Phone,
                    e.Mobile,
                    e.JobTitle,
                    e.Department,
                    e.PrimarySiteId,
                    e.PrimarySite != null ? e.PrimarySite.SiteName : null,
                    e.StartDate,
                    e.EndDate,
                    e.IsActive,
                    e.Notes,
                    e.GeoTrackerID,
                    e.UserId != null,
                    e.UserId,
                    e.PreferredLanguage,
                    e.FloatPersonId,
                    e.FloatLinkedAt,
                    e.FloatLinkMethod
                ))
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return Result.Fail<EmployeeDto>($"Employee with ID {id} not found");
            }

            return Result.Ok(employee);
        }
        catch (Exception ex)
        {
            return Result.Fail<EmployeeDto>($"Error retrieving employee: {ex.Message}");
        }
    }

    public async Task<Result<EmployeeDto>> CreateAsync(CreateEmployeeDto dto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            // Validate that PrimarySiteId exists if provided
            if (dto.PrimarySiteId.HasValue)
            {
                var siteExists = await _context.Sites
                    .AnyAsync(s => s.Id == dto.PrimarySiteId.Value);

                if (!siteExists)
                {
                    return Result.Fail<EmployeeDto>($"Site with ID {dto.PrimarySiteId} not found");
                }
            }

            // Auto-generate EmployeeCode (EMP001, EMP002, etc.) - ignore any value from frontend
            var employeeCode = await GenerateNextEmployeeCodeAsync(tenantId);

            // Validate email uniqueness if creating a user account
            if (dto.CreateUserAccount && !string.IsNullOrWhiteSpace(dto.Email))
            {
                // Check for existing employee with same email
                var existingEmployee = await _context.Employees
                    .AnyAsync(e => e.Email == dto.Email);

                if (existingEmployee)
                {
                    return Result.Fail<EmployeeDto>($"An employee with email '{dto.Email}' already exists");
                }

                // Check for existing user with same email
                var existingUser = await _userManager.Users
                    .Where(u => u.TenantId == tenantId && u.NormalizedEmail == dto.Email.ToUpperInvariant())
                    .FirstOrDefaultAsync();

                if (existingUser != null)
                {
                    return Result.Fail<EmployeeDto>($"A user account with email '{dto.Email}' already exists");
                }
            }

            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeCode = employeeCode,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Email = dto.Email,
                Phone = dto.Phone,
                Mobile = dto.Mobile,
                JobTitle = dto.JobTitle,
                Department = dto.Department,
                PrimarySiteId = dto.PrimarySiteId,
                StartDate = dto.StartDate.HasValue ? DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Utc) : null,
                EndDate = dto.EndDate.HasValue ? DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Utc) : null,
                IsActive = dto.IsActive,
                Notes = dto.Notes,
                PreferredLanguage = dto.PreferredLanguage
            };

            employee.SetGeoTrackerID(dto.GeoTrackerID);

            // Set Float linkage if provided
            if (dto.FloatPersonId.HasValue)
            {
                employee.FloatPersonId = dto.FloatPersonId;
                employee.FloatLinkMethod = "Manual";
                employee.FloatLinkedAt = DateTime.UtcNow;
            }

            _context.Employees.Add(employee);

            User? createdUser = null;

            // Create linked User account if requested and email is provided
            if (dto.CreateUserAccount && !string.IsNullOrWhiteSpace(dto.Email))
            {
                var userCreationResult = await CreateLinkedUserAccountAsync(
                    employee,
                    dto.Email,
                    tenantId,
                    dto.UserRole);

                if (!userCreationResult.Success)
                {
                    return Result.Fail<EmployeeDto>(userCreationResult.Errors);
                }

                createdUser = userCreationResult.Data;
                employee.UserId = createdUser.Id;
            }

            await _context.SaveChangesAsync();

            // Auto-assign training to new employee
            foreach (var assigner in _trainingAssigners)
            {
                try
                {
                    await assigner.AssignNewEmployeeTrainingAsync(
                        tenantId,
                        employee.Id,
                        employee.StartDate);
                }
                catch (Exception trainingEx)
                {
                    _logger.LogWarning(
                        trainingEx,
                        "Failed to auto-assign training for Employee {EmployeeId} via {AssignerType}",
                        employee.Id,
                        assigner.GetType().Name);
                }
            }

            // Send password setup email if user was created
            if (createdUser != null && !string.IsNullOrWhiteSpace(dto.Email))
            {
                try
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(createdUser);
                    await _emailService.SendPasswordSetupEmailAsync(dto.Email, dto.FirstName, token);
                    _logger.LogInformation(
                        "Sent password setup email to {Email} for Employee {EmployeeId}",
                        dto.Email,
                        employee.Id);
                }
                catch (Exception emailEx)
                {
                    // Log but don't fail - the user account was created successfully
                    _logger.LogWarning(
                        emailEx,
                        "Failed to send password setup email to {Email} for Employee {EmployeeId}",
                        dto.Email,
                        employee.Id);
                }
            }

            // Reload with related entities to get site name
            var createdEmployee = await _context.Employees
                .Include(e => e.PrimarySite)
                .FirstAsync(e => e.Id == employee.Id);

            var employeeDto = new EmployeeDto(
                createdEmployee.Id,
                createdEmployee.EmployeeCode,
                createdEmployee.FirstName,
                createdEmployee.LastName,
                createdEmployee.FirstName + " " + createdEmployee.LastName,
                createdEmployee.Email,
                createdEmployee.Phone,
                createdEmployee.Mobile,
                createdEmployee.JobTitle,
                createdEmployee.Department,
                createdEmployee.PrimarySiteId,
                createdEmployee.PrimarySite?.SiteName,
                createdEmployee.StartDate,
                createdEmployee.EndDate,
                createdEmployee.IsActive,
                createdEmployee.Notes,
                createdEmployee.GeoTrackerID,
                createdEmployee.UserId != null,
                createdUser?.Id,
                createdEmployee.PreferredLanguage,
                createdEmployee.FloatPersonId,
                createdEmployee.FloatLinkedAt,
                createdEmployee.FloatLinkMethod
            );

            return Result.Ok(employeeDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating employee");
            return Result.Fail<EmployeeDto>($"Error creating employee: {ex.Message}");
        }
    }

    private async Task<Result<User>> CreateLinkedUserAccountAsync(
        Employee employee,
        string email,
        Guid tenantId,
        string? roleName)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            TenantId = tenantId,
            EmployeeId = employee.Id,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            EmailConfirmed = false, // Will confirm when they set password
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _currentUserService.UserId
        };

        // Generate a secure temporary password
        var tempPassword = GenerateTemporaryPassword();
        var createResult = await _userManager.CreateAsync(user, tempPassword);

        if (!createResult.Succeeded)
        {
            var errors = createResult.Errors.Select(e => e.Description).ToList();
            _logger.LogWarning(
                "Failed to create user account for Employee {EmployeeId}: {Errors}",
                employee.Id,
                string.Join(", ", errors));
            return Result.Fail<User>($"Failed to create user account: {string.Join(", ", errors)}");
        }

        // Assign role
        var role = roleName ?? DefaultUserRole;
        var roleExists = await _roleManager.RoleExistsAsync(role);

        if (!roleExists)
        {
            _logger.LogWarning(
                "Role '{Role}' does not exist, falling back to default role '{DefaultRole}'",
                role,
                DefaultUserRole);
            role = DefaultUserRole;
            roleExists = await _roleManager.RoleExistsAsync(role);
        }

        if (roleExists)
        {
            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                _logger.LogWarning(
                    "Failed to assign role '{Role}' to user {UserId}: {Errors}",
                    role,
                    user.Id,
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }
        }

        _logger.LogInformation(
            "Created User account {UserId} for Employee {EmployeeId} with role '{Role}'",
            user.Id,
            employee.Id,
            role);

        return Result.Ok(user);
    }

    private static string GenerateTemporaryPassword()
    {
        // Generate a secure temporary password that meets ASP.NET Identity requirements
        // This password will never be used - user will reset via email link
        return $"Temp{Guid.NewGuid():N}!Aa1";
    }

    private async Task<string> GenerateNextEmployeeCodeAsync(Guid tenantId)
    {
        // Get ALL existing employee codes for this tenant (including soft-deleted, to avoid reuse)
        var existingCodes = await _context.Employees
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.EmployeeCode != null)
            .Select(e => e.EmployeeCode!)
            .ToListAsync();

        var existingCodeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        // Find the highest numeric portion from EMP-prefixed codes
        int maxNumber = 0;
        foreach (var code in existingCodes)
        {
            // Only consider codes that start with "EMP" followed by digits
            var match = Regex.Match(code, @"^EMP(\d+)$", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var num) && num > maxNumber)
            {
                maxNumber = num;
            }
        }

        // Find next available code (with safety limit to avoid infinite loops)
        const int maxAttempts = 100;
        for (int i = maxNumber + 1; i <= maxNumber + maxAttempts; i++)
        {
            // Format as EMP + 3-digit zero-padded number (e.g., EMP001, EMP022)
            // If number exceeds 999, use more digits (e.g., EMP1000)
            var candidate = i <= 999 ? $"EMP{i:D3}" : $"EMP{i}";

            if (!existingCodeSet.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            $"Unable to generate a unique employee code after {maxAttempts} attempts. " +
            $"Highest existing number was {maxNumber}.");
    }

    public async Task<Result<EmployeeDto>> UpdateAsync(Guid id, UpdateEmployeeDto dto)
    {
        try
        {
            var employee = await _context.Employees
                .Include(e => e.PrimarySite)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                return Result.Fail<EmployeeDto>($"Employee with ID {id} not found");
            }

            // Validate that PrimarySiteId exists if provided
            if (dto.PrimarySiteId.HasValue)
            {
                var siteExists = await _context.Sites
                    .AnyAsync(s => s.Id == dto.PrimarySiteId.Value);

                if (!siteExists)
                {
                    return Result.Fail<EmployeeDto>($"Site with ID {dto.PrimarySiteId} not found");
                }
            }

            // Check for duplicate EmployeeCode (excluding current employee, including soft-deleted records)
            // The database has a unique constraint that covers ALL records, including soft-deleted ones
            var duplicateCodeEmployee = await _context.Employees
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == employee.TenantId && e.EmployeeCode == dto.EmployeeCode && e.Id != id)
                .Select(e => new { e.Id, e.FirstName, e.LastName, e.IsDeleted })
                .FirstOrDefaultAsync();

            if (duplicateCodeEmployee != null)
            {
                if (duplicateCodeEmployee.IsDeleted)
                {
                    return Result.Fail<EmployeeDto>(
                        $"Employee code '{dto.EmployeeCode}' was previously assigned to deleted employee " +
                        $"{duplicateCodeEmployee.FirstName} {duplicateCodeEmployee.LastName}. " +
                        "Please choose a different code or permanently delete the former employee record first.");
                }
                return Result.Fail<EmployeeDto>($"Employee code '{dto.EmployeeCode}' is already in use by an active employee.");
            }

            var emailChanged = employee.Email != dto.Email;

            // Check email uniqueness if changed
            if (emailChanged && !string.IsNullOrWhiteSpace(dto.Email))
            {
                var emailExists = await _context.Employees
                    .AnyAsync(e => e.Email == dto.Email && e.Id != id);

                if (emailExists)
                {
                    return Result.Fail<EmployeeDto>($"An employee with email '{dto.Email}' already exists");
                }
            }

            employee.EmployeeCode = dto.EmployeeCode;
            employee.FirstName = dto.FirstName;
            employee.LastName = dto.LastName;
            employee.Email = dto.Email;
            employee.Phone = dto.Phone;
            employee.Mobile = dto.Mobile;
            employee.JobTitle = dto.JobTitle;
            employee.Department = dto.Department;
            employee.PrimarySiteId = dto.PrimarySiteId;
            employee.StartDate = dto.StartDate.HasValue ? DateTime.SpecifyKind(dto.StartDate.Value, DateTimeKind.Utc) : null;
            employee.EndDate = dto.EndDate.HasValue ? DateTime.SpecifyKind(dto.EndDate.Value, DateTimeKind.Utc) : null;
            employee.IsActive = dto.IsActive;
            employee.Notes = dto.Notes;
            employee.SetGeoTrackerID(dto.GeoTrackerID);

            // Update preferred language if provided
            if (!string.IsNullOrWhiteSpace(dto.PreferredLanguage))
            {
                employee.PreferredLanguage = dto.PreferredLanguage;
            }

            // Handle Float linkage changes
            if (dto.FloatPersonId != employee.FloatPersonId)
            {
                if (dto.FloatPersonId.HasValue)
                {
                    // Setting or changing Float Person ID - set manual linkage
                    employee.FloatPersonId = dto.FloatPersonId;
                    employee.FloatLinkMethod = "Manual";
                    employee.FloatLinkedAt = DateTime.UtcNow;
                }
                else
                {
                    // Clearing Float Person ID - clear linkage fields
                    employee.FloatPersonId = null;
                    employee.FloatLinkMethod = null;
                    employee.FloatLinkedAt = null;
                }
            }

            // Sync User account if email changed and employee has a linked user
            if (emailChanged && employee.UserId.HasValue)
            {
                var syncResult = await SyncUserEmailAsync(employee.UserId.Value, dto.Email);
                if (!syncResult.Success)
                {
                    _logger.LogWarning(
                        "Failed to sync email change to User account for Employee {EmployeeId}: {Error}",
                        employee.Id,
                        string.Join(", ", syncResult.Errors));
                }
            }

            // Sync name changes to linked User if exists
            if (employee.UserId.HasValue)
            {
                await SyncUserNameAsync(employee.UserId.Value, dto.FirstName, dto.LastName);
            }

            await _context.SaveChangesAsync();

            // Reload to get updated related entity name
            await _context.Employees
                .Entry(employee)
                .Reference(e => e.PrimarySite)
                .LoadAsync();

            var employeeDto = new EmployeeDto(
                employee.Id,
                employee.EmployeeCode,
                employee.FirstName,
                employee.LastName,
                employee.FirstName + " " + employee.LastName,
                employee.Email,
                employee.Phone,
                employee.Mobile,
                employee.JobTitle,
                employee.Department,
                employee.PrimarySiteId,
                employee.PrimarySite?.SiteName,
                employee.StartDate,
                employee.EndDate,
                employee.IsActive,
                employee.Notes,
                employee.GeoTrackerID,
                employee.UserId != null,
                employee.UserId,
                employee.PreferredLanguage,
                employee.FloatPersonId,
                employee.FloatLinkedAt,
                employee.FloatLinkMethod
            );

            return Result.Ok(employeeDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee {EmployeeId}", id);
            return Result.Fail<EmployeeDto>($"Error updating employee: {ex.Message}");
        }
    }

    private async Task<Result> SyncUserEmailAsync(Guid userId, string? newEmail)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return Result.Fail($"Linked user account {userId} not found");
            }

            if (string.IsNullOrWhiteSpace(newEmail))
            {
                // Don't clear the email - user needs it to log in
                _logger.LogWarning(
                    "Cannot clear email for User {UserId} - email is required for login",
                    userId);
                return Result.Ok();
            }

            // Check if new email conflicts with another user
            var existingUser = await _userManager.FindByEmailAsync(newEmail);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                return Result.Fail($"Email '{newEmail}' is already in use by another user");
            }

            user.Email = newEmail;
            user.UserName = newEmail;
            user.NormalizedEmail = newEmail.ToUpperInvariant();
            user.NormalizedUserName = newEmail.ToUpperInvariant();
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = _currentUserService.UserId;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Result.Fail(result.Errors.Select(e => e.Description).ToList());
            }

            _logger.LogInformation(
                "Synced email change to User {UserId}: {NewEmail}",
                userId,
                newEmail);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing email to User {UserId}", userId);
            return Result.Fail($"Error syncing email: {ex.Message}");
        }
    }

    private async Task SyncUserNameAsync(Guid userId, string firstName, string lastName)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return;
            }

            if (user.FirstName != firstName || user.LastName != lastName)
            {
                user.FirstName = firstName;
                user.LastName = lastName;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = _currentUserService.UserId;

                await _userManager.UpdateAsync(user);

                _logger.LogInformation(
                    "Synced name change to User {UserId}: {FirstName} {LastName}",
                    userId,
                    firstName,
                    lastName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error syncing name to User {UserId}", userId);
        }
    }

    public async Task<Result> DeleteAsync(Guid id)
    {
        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                return Result.Fail($"Employee with ID {id} not found");
            }

            // Check for active supervisor assignments (as supervisor)
            var supervisorCount = await _context.SupervisorAssignments
                .CountAsync(sa => sa.SupervisorEmployeeId == id);

            if (supervisorCount > 0)
            {
                return Result.Fail(
                    $"This employee is a Supervisor with {supervisorCount} assigned operator(s). Please remove all operator assignments before deleting.");
            }

            // Check for active supervisor assignments (as operator)
            var operatorCount = await _context.SupervisorAssignments
                .CountAsync(sa => sa.OperatorEmployeeId == id);

            if (operatorCount > 0)
            {
                return Result.Fail(
                    $"This employee is assigned to {operatorCount} supervisor(s). Please remove them from all supervisor assignments before deleting.");
            }

            // Soft delete Employee
            employee.IsDeleted = true;
            employee.IsActive = false;

            // Deactivate linked User account
            if (employee.UserId.HasValue)
            {
                await DeactivateLinkedUserAsync(employee.UserId.Value);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted Employee {EmployeeId}", id);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee {EmployeeId}", id);
            return Result.Fail($"Error deleting employee: {ex.Message}");
        }
    }

    public async Task<Result<List<EmployeeDto>>> GetUnlinkedAsync()
    {
        try
        {
            var employees = await _context.Employees
                .Include(e => e.PrimarySite)
                .Where(e => e.UserId == null && e.IsActive)
                .OrderBy(e => e.FirstName)
                .ThenBy(e => e.LastName)
                .Select(e => new EmployeeDto(
                    e.Id,
                    e.EmployeeCode,
                    e.FirstName,
                    e.LastName,
                    e.FirstName + " " + e.LastName,
                    e.Email,
                    e.Phone,
                    e.Mobile,
                    e.JobTitle,
                    e.Department,
                    e.PrimarySiteId,
                    e.PrimarySite != null ? e.PrimarySite.SiteName : null,
                    e.StartDate,
                    e.EndDate,
                    e.IsActive,
                    e.Notes,
                    e.GeoTrackerID,
                    false,
                    null,
                    e.PreferredLanguage,
                    e.FloatPersonId,
                    e.FloatLinkedAt,
                    e.FloatLinkMethod
                ))
                .ToListAsync();

            return Result.Ok(employees);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<EmployeeDto>>($"Error retrieving unlinked employees: {ex.Message}");
        }
    }

    private async Task DeactivateLinkedUserAsync(Guid userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning("Linked user account {UserId} not found for deactivation", userId);
                return;
            }

            user.IsActive = false;
            user.LockoutEnd = DateTimeOffset.MaxValue; // Lock them out permanently
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = _currentUserService.UserId;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Deactivated User account {UserId} due to Employee deletion",
                    userId);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to deactivate User {UserId}: {Errors}",
                    userId,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deactivating User {UserId}", userId);
        }
    }

    public async Task<Result> ResendInviteAsync(Guid employeeId)
    {
        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
            {
                return Result.Fail($"Employee with ID {employeeId} not found");
            }

            if (!employee.UserId.HasValue)
            {
                return Result.Fail("This employee does not have a linked user account. Cannot resend invite.");
            }

            if (string.IsNullOrWhiteSpace(employee.Email))
            {
                return Result.Fail("This employee does not have an email address. Cannot send invite.");
            }

            var user = await _userManager.FindByIdAsync(employee.UserId.Value.ToString());
            if (user == null)
            {
                return Result.Fail("The linked user account could not be found. The account may have been deleted.");
            }

            if (!user.IsActive)
            {
                return Result.Fail("The user account is deactivated. Please reactivate the account first.");
            }

            // Generate a new password reset token and send the setup email
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _emailService.SendPasswordSetupEmailAsync(employee.Email, employee.FirstName, token);

            _logger.LogInformation(
                "Resent password setup email to {Email} for Employee {EmployeeId}",
                employee.Email,
                employee.Id);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending invite for employee {EmployeeId}", employeeId);
            return Result.Fail($"Error resending invite: {ex.Message}");
        }
    }

    public async Task<Result<EmployeeDto>> LinkToUserAsync(Guid employeeId, LinkEmployeeToUserDto dto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            // Get the employee
            var employee = await _context.Employees
                .Include(e => e.PrimarySite)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
            {
                return Result.Fail<EmployeeDto>($"Employee with ID {employeeId} not found");
            }

            if (employee.UserId.HasValue)
            {
                return Result.Fail<EmployeeDto>("This employee is already linked to a user account");
            }

            // Get the user
            var user = await _userManager.Users
                .Where(u => u.Id == dto.UserId && u.TenantId == tenantId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return Result.Fail<EmployeeDto>($"User with ID {dto.UserId} not found");
            }

            if (user.EmployeeId.HasValue)
            {
                return Result.Fail<EmployeeDto>("This user is already linked to another employee");
            }

            // Create the bidirectional link
            employee.UserId = user.Id;
            user.EmployeeId = employee.Id;
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = _currentUserService.UserId;

            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Linked Employee {EmployeeId} to User {UserId}",
                employeeId, dto.UserId);

            var employeeDto = new EmployeeDto(
                employee.Id,
                employee.EmployeeCode,
                employee.FirstName,
                employee.LastName,
                employee.FirstName + " " + employee.LastName,
                employee.Email,
                employee.Phone,
                employee.Mobile,
                employee.JobTitle,
                employee.Department,
                employee.PrimarySiteId,
                employee.PrimarySite?.SiteName,
                employee.StartDate,
                employee.EndDate,
                employee.IsActive,
                employee.Notes,
                employee.GeoTrackerID,
                true,
                user.Id,
                employee.PreferredLanguage,
                employee.FloatPersonId,
                employee.FloatLinkedAt,
                employee.FloatLinkMethod
            );

            return Result.Ok(employeeDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking employee {EmployeeId} to user", employeeId);
            return Result.Fail<EmployeeDto>($"Error linking employee to user: {ex.Message}");
        }
    }

    public async Task<Result<EmployeeDto>> CreateUserForEmployeeAsync(Guid employeeId, CreateUserForEmployeeDto dto)
    {
        try
        {
            var tenantId = _currentUserService.TenantId;

            // Get the employee
            var employee = await _context.Employees
                .Include(e => e.PrimarySite)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
            {
                return Result.Fail<EmployeeDto>($"Employee with ID {employeeId} not found");
            }

            if (employee.UserId.HasValue)
            {
                return Result.Fail<EmployeeDto>("This employee already has a linked user account");
            }

            if (string.IsNullOrWhiteSpace(employee.Email))
            {
                return Result.Fail<EmployeeDto>("Employee must have an email address to create a user account");
            }

            // Check if email is already in use
            var existingUser = await _userManager.Users
                .Where(u => u.TenantId == tenantId && u.NormalizedEmail == employee.Email.ToUpperInvariant())
                .FirstOrDefaultAsync();

            if (existingUser != null)
            {
                return Result.Fail<EmployeeDto>($"A user with email '{employee.Email}' already exists");
            }

            // Validate roles
            if (dto.RoleIds == null || dto.RoleIds.Count == 0)
            {
                return Result.Fail<EmployeeDto>("At least one role must be specified");
            }

            var roles = await _roleManager.Roles
                .Where(r => dto.RoleIds.Contains(r.Id))
                .ToListAsync();

            if (roles.Count != dto.RoleIds.Count)
            {
                return Result.Fail<EmployeeDto>("One or more role IDs are invalid");
            }

            // Create the user using the existing helper method
            var userCreationResult = await CreateLinkedUserAccountAsync(
                employee,
                employee.Email,
                tenantId,
                roles.First().Name);

            if (!userCreationResult.Success)
            {
                return Result.Fail<EmployeeDto>(userCreationResult.Errors);
            }

            var user = userCreationResult.Data!;
            employee.UserId = user.Id;

            // Add any additional roles (first one was added by CreateLinkedUserAccountAsync)
            foreach (var role in roles.Skip(1))
            {
                await _userManager.AddToRoleAsync(user, role.Name!);
            }

            await _context.SaveChangesAsync();

            // Send password setup email
            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                await _emailService.SendPasswordSetupEmailAsync(employee.Email, employee.FirstName, token);
                _logger.LogInformation(
                    "Sent password setup email to {Email} for new User {UserId}",
                    employee.Email, user.Id);
            }
            catch (Exception emailEx)
            {
                _logger.LogWarning(
                    emailEx,
                    "Failed to send password setup email to {Email} for User {UserId}",
                    employee.Email, user.Id);
            }

            var employeeDto = new EmployeeDto(
                employee.Id,
                employee.EmployeeCode,
                employee.FirstName,
                employee.LastName,
                employee.FirstName + " " + employee.LastName,
                employee.Email,
                employee.Phone,
                employee.Mobile,
                employee.JobTitle,
                employee.Department,
                employee.PrimarySiteId,
                employee.PrimarySite?.SiteName,
                employee.StartDate,
                employee.EndDate,
                employee.IsActive,
                employee.Notes,
                employee.GeoTrackerID,
                true,
                user.Id,
                employee.PreferredLanguage,
                employee.FloatPersonId,
                employee.FloatLinkedAt,
                employee.FloatLinkMethod
            );

            return Result.Ok(employeeDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user for employee {EmployeeId}", employeeId);
            return Result.Fail<EmployeeDto>($"Error creating user: {ex.Message}");
        }
    }

    public async Task<Result> UnlinkUserAsync(Guid employeeId)
    {
        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
            {
                return Result.Fail($"Employee with ID {employeeId} not found");
            }

            if (!employee.UserId.HasValue)
            {
                return Result.Fail("This employee is not linked to any user account");
            }

            var user = await _userManager.FindByIdAsync(employee.UserId.Value.ToString());
            if (user != null)
            {
                user.EmployeeId = null;
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = _currentUserService.UserId;
                await _userManager.UpdateAsync(user);
            }

            var previousUserId = employee.UserId;
            employee.UserId = null;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Unlinked Employee {EmployeeId} from User {UserId}",
                employeeId, previousUserId);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking user from employee {EmployeeId}", employeeId);
            return Result.Fail($"Error unlinking user: {ex.Message}");
        }
    }
}
