using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Identity;

namespace QuantumBuild.Core.Infrastructure.Persistence;

/// <summary>
/// Seeds initial data for the application including permissions, roles, tenants, and users
/// </summary>
public static class DataSeeder
{
    /// <summary>
    /// Default tenant ID for QUANTUMBUILD
    /// </summary>
    public static readonly Guid DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>
    /// Seed all initial data
    /// </summary>
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<object>>();

        try
        {
            var context = services.GetRequiredService<DbContext>();
            var userManager = services.GetRequiredService<UserManager<User>>();
            var roleManager = services.GetRequiredService<RoleManager<Role>>();

            await SeedTenantsAsync(context, logger);
            await SeedPermissionsAsync(context, logger);
            await SeedRolesAsync(context, roleManager, logger);
            await SeedRolePermissionsAsync(context, logger);
            await SeedAdminUserAsync(userManager, roleManager, logger);
            await EnsureAdminEmployeeAsync(context, userManager, logger);

            logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }

    private static async Task SeedTenantsAsync(DbContext context, ILogger logger)
    {
        var tenants = context.Set<Tenant>();

        if (await tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == DefaultTenantId))
        {
            logger.LogInformation("Default tenant already exists, skipping");
            return;
        }

        var tenant = new Tenant
        {
            Id = DefaultTenantId,
            Name = "QUANTUMBUILD",
            Code = "QUANTUMBUILD",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        await tenants.AddAsync(tenant);
        await context.SaveChangesAsync();
        logger.LogInformation("Created default tenant: {TenantName}", tenant.Name);
    }

    private static async Task SeedPermissionsAsync(DbContext context, ILogger logger)
    {
        var permissions = context.Set<Permission>();
        var existingPermissions = await permissions
            .IgnoreQueryFilters()
            .Select(p => p.Name)
            .ToListAsync();

        var allPermissions = Permissions.GetAll().ToList();
        var newPermissions = new List<Permission>();

        foreach (var permissionName in allPermissions)
        {
            if (existingPermissions.Contains(permissionName))
                continue;

            var moduleName = Permissions.GetModuleName(permissionName);
            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                Name = permissionName,
                Module = moduleName,
                Description = GetPermissionDescription(permissionName),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            newPermissions.Add(permission);
        }

        if (newPermissions.Count > 0)
        {
            await permissions.AddRangeAsync(newPermissions);
            await context.SaveChangesAsync();
            logger.LogInformation("Created {Count} new permissions", newPermissions.Count);
        }
        else
        {
            logger.LogInformation("All permissions already exist, skipping");
        }
    }

    private static async Task SeedRolesAsync(DbContext context, RoleManager<Role> roleManager, ILogger logger)
    {
        var rolesToCreate = new[]
        {
            new { Name = "Admin", Description = "Full system administrator with all permissions" },
            new { Name = "Finance", Description = "Finance team with view permissions" },
            new { Name = "OfficeStaff", Description = "Office staff with core and learnings access" },
            new { Name = "SiteManager", Description = "Site manager with core and learnings access" },
            new { Name = "WarehouseStaff", Description = "Warehouse staff with core and learnings permissions" },
            new { Name = "Operative", Description = "General Operative staff with core and learnings permissions" }
        };

        foreach (var roleInfo in rolesToCreate)
        {
            var existingRole = await roleManager.FindByNameAsync(roleInfo.Name);
            if (existingRole != null)
            {
                logger.LogInformation("Role {RoleName} already exists, skipping", roleInfo.Name);
                continue;
            }

            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name = roleInfo.Name,
                NormalizedName = roleInfo.Name.ToUpperInvariant(),
                Description = roleInfo.Description,
                IsSystemRole = true,
                IsActive = true,
                TenantId = null, // System-wide roles
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            var result = await roleManager.CreateAsync(role);
            if (result.Succeeded)
            {
                logger.LogInformation("Created role: {RoleName}", roleInfo.Name);
            }
            else
            {
                logger.LogWarning("Failed to create role {RoleName}: {Errors}",
                    roleInfo.Name, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedRolePermissionsAsync(DbContext context, ILogger logger)
    {
        var roles = await context.Set<Role>()
            .Include(r => r.RolePermissions)
            .ToListAsync();

        var allPermissions = await context.Set<Permission>()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted)
            .ToListAsync();

        var rolePermissions = context.Set<RolePermission>();
        var newAssignments = new List<RolePermission>();

        foreach (var role in roles)
        {
            var permissionsForRole = GetPermissionsForRole(role.Name!, allPermissions);
            var existingPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

            foreach (var permission in permissionsForRole)
            {
                if (existingPermissionIds.Contains(permission.Id))
                    continue;

                newAssignments.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id
                });
            }
        }

        if (newAssignments.Count > 0)
        {
            await rolePermissions.AddRangeAsync(newAssignments);
            await context.SaveChangesAsync();
            logger.LogInformation("Created {Count} new role-permission assignments", newAssignments.Count);
        }
        else
        {
            logger.LogInformation("All role permissions already assigned, skipping");
        }
    }

    private static async Task SeedAdminUserAsync(UserManager<User> userManager, RoleManager<Role> roleManager, ILogger logger)
    {
        const string adminEmail = "admin@quantumbuild.ai";
        const string adminPassword = "Admin123!";

        var existingUser = await userManager.FindByEmailAsync(adminEmail);
        if (existingUser != null)
        {
            logger.LogInformation("Admin user already exists, skipping");
            return;
        }

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            FirstName = "System",
            LastName = "Administrator",
            TenantId = DefaultTenantId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            logger.LogInformation("Created admin user: {Email}", adminEmail);

            // Assign Admin role
            var adminRole = await roleManager.FindByNameAsync("Admin");
            if (adminRole != null)
            {
                var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
                if (roleResult.Succeeded)
                {
                    logger.LogInformation("Assigned Admin role to user: {Email}", adminEmail);
                }
                else
                {
                    logger.LogWarning("Failed to assign Admin role: {Errors}",
                        string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
        }
        else
        {
            logger.LogWarning("Failed to create admin user: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private static IEnumerable<Permission> GetPermissionsForRole(string roleName, List<Permission> allPermissions)
    {
        return roleName switch
        {
            "Admin" => allPermissions, // All permissions

            "Finance" => allPermissions.Where(p =>
                p.Name.EndsWith(".View") ||
                p.Name == Permissions.ToolboxTalks.ViewReports),

            "OfficeStaff" => allPermissions.Where(p =>
                p.Name == Permissions.ToolboxTalks.View ||
                p.Name == Permissions.ToolboxTalks.Create ||
                p.Name == Permissions.ToolboxTalks.Edit ||
                p.Name == Permissions.ToolboxTalks.Schedule ||
                p.Name == Permissions.ToolboxTalks.ViewReports ||
                p.Name == Permissions.Core.ManageCompanies),

            "SiteManager" => allPermissions.Where(p =>
                p.Name.StartsWith("ToolboxTalks.") ||
                p.Name == Permissions.Core.ManageSites ||
                p.Name == Permissions.Core.ManageEmployees),

            "WarehouseStaff" => allPermissions.Where(p =>
                p.Name == Permissions.ToolboxTalks.View ||
                p.Name == Permissions.ToolboxTalks.ViewReports),

            _ => Enumerable.Empty<Permission>()
        };
    }

    private static string GetPermissionDescription(string permissionName)
    {
        return permissionName switch
        {
            // Toolbox Talks
            Permissions.ToolboxTalks.View => "View learnings",
            Permissions.ToolboxTalks.Create => "Create learnings",
            Permissions.ToolboxTalks.Edit => "Edit learnings",
            Permissions.ToolboxTalks.Delete => "Delete learnings",
            Permissions.ToolboxTalks.Schedule => "Schedule learnings",
            Permissions.ToolboxTalks.ViewReports => "View learnings reports",
            Permissions.ToolboxTalks.Admin => "Full learnings administration",

            // Core
            Permissions.Core.ManageSites => "Manage sites",
            Permissions.Core.ManageEmployees => "Manage employees",
            Permissions.Core.ManageCompanies => "Manage companies and contacts",
            Permissions.Core.ManageUsers => "Manage user accounts",
            Permissions.Core.ManageRoles => "Manage roles and permissions",
            Permissions.Core.Admin => "Full core system administration",

            _ => $"Permission: {permissionName}"
        };
    }

    private static async Task EnsureAdminEmployeeAsync(DbContext context, UserManager<User> userManager, ILogger logger)
    {
        var employees = context.Set<Employee>();
        var adminEmail = "admin@quantumbuild.ai";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            logger.LogInformation("Admin user not found, skipping employee creation");
            return;
        }

        // Check if admin already has an employee record
        if (adminUser.EmployeeId != null)
        {
            logger.LogInformation("Admin user already linked to employee, skipping");
            return;
        }

        var adminEmployee = new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = DefaultTenantId,
            EmployeeCode = "EMP-ADMIN",
            FirstName = adminUser.FirstName ?? "Admin",
            LastName = adminUser.LastName ?? "User",
            Email = adminEmail,
            JobTitle = "Administrator",
            Department = "Management",
            IsActive = true,
            UserId = adminUser.Id.ToString(),
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        await employees.AddAsync(adminEmployee);

        // Link user to employee (bidirectional)
        adminUser.EmployeeId = adminEmployee.Id;
        await userManager.UpdateAsync(adminUser);

        await context.SaveChangesAsync();
        logger.LogInformation("Created and linked admin employee record for {Email}", adminEmail);
    }
}
