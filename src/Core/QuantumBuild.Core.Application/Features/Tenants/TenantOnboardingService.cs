using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Core.Application.Features.Tenants;

public class TenantOnboardingService(
    UserManager<User> userManager,
    RoleManager<Role> roleManager,
    ICoreDbContext context,
    IEmailService emailService,
    ILogger<TenantOnboardingService> logger) : ITenantOnboardingService
{
    private static readonly string[] DefaultRoleNames = ["Admin", "Supervisor", "Operator"];

    public async Task<Result> ProvisionTenantAsync(Guid tenantId, string contactEmail, string contactName)
    {
        try
        {
            var roleIds = await EnsureDefaultRolesExistAsync();
            if (roleIds == null)
                return Result.Fail("Failed to verify default roles exist. Ensure database has been seeded.");

            var (user, error) = await CreateAdminUserAsync(tenantId, contactEmail, contactName);
            if (user == null)
                return Result.Fail(error!);

            var adminRoleId = roleIds["Admin"];
            var assignResult = await AssignRoleAsync(user, adminRoleId);
            if (!assignResult)
                return Result.Fail("Failed to assign Admin role to tenant admin user.");

            var employee = await CreateLinkedEmployeeAsync(tenantId, user, contactEmail, contactName);

            await SendWelcomeEmailAsync(user);

            logger.LogInformation(
                "Tenant {TenantId} onboarding complete: user {UserId}, employee {EmployeeId}",
                tenantId, user.Id, employee.Id);

            return Result.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during tenant onboarding for {TenantId}", tenantId);
            return Result.Fail($"Tenant onboarding failed: {ex.Message}");
        }
    }

    private async Task<Dictionary<string, Guid>?> EnsureDefaultRolesExistAsync()
    {
        var result = new Dictionary<string, Guid>();

        foreach (var roleName in DefaultRoleNames)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                logger.LogError("Required system role '{RoleName}' not found", roleName);
                return null;
            }
            result[roleName] = role.Id;
        }

        return result;
    }

    private async Task<(User? User, string? Error)> CreateAdminUserAsync(
        Guid tenantId, string email, string contactName)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null)
            return (null, $"A user with email '{email}' already exists.");

        var (firstName, lastName) = ParseContactName(contactName);

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            TenantId = tenantId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        // Create with a temporary password (user will set their own via email link)
        var tempPassword = GenerateTemporaryPassword();
        var createResult = await userManager.CreateAsync(user, tempPassword);

        if (!createResult.Succeeded)
        {
            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
            logger.LogError("Failed to create admin user for tenant {TenantId}: {Errors}", tenantId, errors);
            return (null, $"Failed to create admin user: {errors}");
        }

        logger.LogInformation("Created admin user {Email} for tenant {TenantId}", email, tenantId);
        return (user, null);
    }

    private async Task<bool> AssignRoleAsync(User user, Guid adminRoleId)
    {
        var role = await roleManager.FindByIdAsync(adminRoleId.ToString());
        if (role?.Name == null)
            return false;

        var result = await userManager.AddToRoleAsync(user, role.Name);
        if (!result.Succeeded)
        {
            logger.LogError("Failed to assign Admin role to user {UserId}: {Errors}",
                user.Id, string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        return true;
    }

    private async Task<Employee> CreateLinkedEmployeeAsync(
        Guid tenantId, User user, string email, string contactName)
    {
        var (firstName, lastName) = ParseContactName(contactName);

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EmployeeCode = await GenerateFirstEmployeeCodeAsync(tenantId),
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            JobTitle = "Administrator",
            Department = "Management",
            IsActive = true,
            UserId = user.Id,
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        context.Employees.Add(employee);

        user.EmployeeId = employee.Id;
        await userManager.UpdateAsync(user);

        await context.SaveChangesAsync();

        logger.LogInformation("Created and linked employee {EmployeeId} for user {UserId}", employee.Id, user.Id);
        return employee;
    }

    private async Task SendWelcomeEmailAsync(User user)
    {
        try
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            await emailService.SendPasswordSetupEmailAsync(
                user.Email!,
                user.FirstName,
                token);

            logger.LogInformation("Sent welcome email to {Email}", user.Email);
        }
        catch (Exception ex)
        {
            // Don't fail the entire onboarding if email fails
            logger.LogWarning(ex, "Failed to send welcome email to {Email}. User was created successfully.", user.Email);
        }
    }

    private async Task<string> GenerateFirstEmployeeCodeAsync(Guid tenantId)
    {
        var hasEmployees = await context.Employees
            .IgnoreQueryFilters()
            .AnyAsync(e => e.TenantId == tenantId);

        if (!hasEmployees)
            return "EMP001";

        // Fallback: find max code and increment
        var maxCode = await context.Employees
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.EmployeeCode.StartsWith("EMP"))
            .Select(e => e.EmployeeCode)
            .MaxAsync();

        if (maxCode != null && int.TryParse(maxCode.Replace("EMP", ""), out var num))
            return $"EMP{(num + 1):D3}";

        return "EMP001";
    }

    private static (string FirstName, string LastName) ParseContactName(string contactName)
    {
        var parts = contactName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => ("Admin", "User"),
            1 => (parts[0], ""),
            _ => (parts[0], parts[1])
        };
    }

    private static string GenerateTemporaryPassword()
    {
        // Meets Identity requirements: 8+ chars, upper, lower, digit, non-alphanumeric
        return $"Tmp{Guid.NewGuid():N}"[..16] + "!A1";
    }
}
