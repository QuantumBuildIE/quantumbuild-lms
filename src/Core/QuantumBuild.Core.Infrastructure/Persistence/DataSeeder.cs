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
            await SeedTestUsersAsync(userManager, roleManager, logger);
            await SeedSitesAsync(context, logger);

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
            new { Name = "OfficeStaff", Description = "Office staff with core and toolbox talks access" },
            new { Name = "SiteManager", Description = "Site manager with core and toolbox talks access" },
            new { Name = "WarehouseStaff", Description = "Warehouse staff with core and toolbox talks permissions" }
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

    private static async Task SeedTestUsersAsync(UserManager<User> userManager, RoleManager<Role> roleManager, ILogger logger)
    {
        var testUsers = new[]
        {
            new { Email = "warehouse@quantumbuild.ai", Password = "Warehouse123!", FirstName = "John", LastName = "Warehouse", Role = "WarehouseStaff" },
            new { Email = "sitemanager@quantumbuild.ai", Password = "SiteManager123!", FirstName = "Sarah", LastName = "Site", Role = "SiteManager" },
            new { Email = "office@quantumbuild.ai", Password = "Office123!", FirstName = "Mike", LastName = "Office", Role = "OfficeStaff" },
            new { Email = "finance@quantumbuild.ai", Password = "Finance123!", FirstName = "Emma", LastName = "Finance", Role = "Finance" }
        };

        foreach (var testUser in testUsers)
        {
            var existingUser = await userManager.FindByEmailAsync(testUser.Email);
            if (existingUser != null)
            {
                logger.LogInformation("Test user {Email} already exists, skipping", testUser.Email);
                continue;
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = testUser.Email,
                Email = testUser.Email,
                EmailConfirmed = true,
                FirstName = testUser.FirstName,
                LastName = testUser.LastName,
                TenantId = DefaultTenantId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            var result = await userManager.CreateAsync(user, testUser.Password);
            if (result.Succeeded)
            {
                logger.LogInformation("Created test user: {Email}", testUser.Email);

                // Assign role
                var role = await roleManager.FindByNameAsync(testUser.Role);
                if (role != null)
                {
                    var roleResult = await userManager.AddToRoleAsync(user, testUser.Role);
                    if (roleResult.Succeeded)
                    {
                        logger.LogInformation("Assigned {Role} role to user: {Email}", testUser.Role, testUser.Email);
                    }
                    else
                    {
                        logger.LogWarning("Failed to assign {Role} role to {Email}: {Errors}",
                            testUser.Role, testUser.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                }
            }
            else
            {
                logger.LogWarning("Failed to create test user {Email}: {Errors}",
                    testUser.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedSitesAsync(DbContext context, ILogger logger)
    {
        var sites = context.Set<Site>();

        if (await sites.IgnoreQueryFilters().AnyAsync())
        {
            logger.LogInformation("Sites already exist, skipping site seeding");
            return;
        }

        var sitesToCreate = new List<Site>
        {
            new Site
            {
                Id = Guid.Parse("85dd7ee6-5546-4dfb-ae85-71a00416dc5a"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE000",
                SiteName = "Quantum Build",
                IsActive = true,
                Notes = "Site Type: HQ",
                GeofenceRadiusMeters = 100,
                Latitude = 52.47102500m,
                Longitude = -6.31557800m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("37362382-54ff-42a6-ae5b-12a2bba3b99e"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE001",
                SiteName = "QUANTUMBUILD HQ",
                IsActive = true,
                Notes = "Site Type: HQ",
                GeofenceRadiusMeters = 100,
                Latitude = 52.69179300m,
                Longitude = -6.27027500m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("45e49c60-03c5-45e4-8057-565979111a81"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE002",
                SiteName = "South West Gate",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.32552500m,
                Longitude = -6.34152600m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("5ddf4904-d644-479d-a93b-077a343672fa"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE003",
                SiteName = "Marmalade Lane",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.28082400m,
                Longitude = -6.24009200m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("ecb10c90-0162-4d62-b315-5d3b56ab8fd0"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE004",
                SiteName = "Rathbourne Crossing",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.37703000m,
                Longitude = -6.33170700m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("202904ea-9d8e-445a-a6f5-119552f5df9e"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE005",
                SiteName = "Castleforbes Prem Inn",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.35036700m,
                Longitude = -6.23446700m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("fa97b293-b0d9-476a-81d5-1318a4d891d5"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE006",
                SiteName = "Angem",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.27231300m,
                Longitude = -6.15201000m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("3e0c1b9d-9f34-481f-bcad-462fa3cf8f5b"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE007",
                SiteName = "Oscar Trainer Road",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.39712600m,
                Longitude = -6.23325800m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("2a039849-252b-4810-8dae-91be76237f1d"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE008",
                SiteName = "Eden",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 51.89409200m,
                Longitude = -8.41367700m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("a56b0a79-8041-4483-83cf-21c8bf24e034"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE009",
                SiteName = "Jacobs Island",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 51.88378000m,
                Longitude = -8.39291900m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("737e78f9-a3e4-41ce-b7be-d327f9172812"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE010",
                SiteName = "Ford",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 51.89980800m,
                Longitude = -8.44035800m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("2af4c031-1290-4ad4-976f-2a1bd883b344"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE011",
                SiteName = "Tile 6",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33234100m,
                Longitude = -6.42659200m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("c8fc0097-5821-4a0a-97e6-c02efd4a563a"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE012",
                SiteName = "Montrose",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.31760900m,
                Longitude = -6.22713100m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("1edb70e6-7774-4bd2-9fed-1f7d9d979caa"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE013",
                SiteName = "Donore",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33516900m,
                Longitude = -6.28571300m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("4ed4ba88-ffce-4a0f-8371-14e54dcc8d87"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE014",
                SiteName = "25 Edenticullo Road",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 54.44607510m,
                Longitude = -6.06161020m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("c2cd386c-03dc-4ff4-8464-a687752ed021"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE015",
                SiteName = "55 Mount Merrion Phase 1",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.29620850m,
                Longitude = -6.21191260m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("468bf48c-0f89-41c4-b5af-3fed390c3378"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE016",
                SiteName = "Amgen, Pottery Road, Dun Laoghaire",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.27340450m,
                Longitude = -6.15335410m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("a3bdb747-b17a-4e9b-9981-3dd12d11476c"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE017",
                SiteName = "Ardfallen, Dalkey,",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.27468450m,
                Longitude = -6.10515250m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("48ea763a-230d-4b88-bf14-853f479f979c"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE018",
                SiteName = "Donore Avenue",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33541720m,
                Longitude = -6.28420990m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("dc36f3cb-08c3-446b-98f2-c2b053b5b8e7"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE019",
                SiteName = "Kainos",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 54.59205090m,
                Longitude = -5.93191480m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("9a864920-745e-40f3-b0d1-7945d55323fb"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE020",
                SiteName = "Attenuation Tanks, Ballycastle Shared Education Campus",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 55.20294780m,
                Longitude = -6.25266980m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("8a58773f-35a9-4a45-86d5-8a2f4ccfd228"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE021",
                SiteName = "Attenuation Tanks, Ballymena",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 54.86852300m,
                Longitude = -6.26751890m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("e3229a0b-f29c-43e8-a102-43ea3bb46f19"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE022",
                SiteName = "24 - 28 Fosters Avenue",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.30181700m,
                Longitude = -6.21133350m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("595f548b-f56c-48b5-9564-68fa9459381b"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE023",
                SiteName = "Balconies at Hartfield Place, Swords, Blocks F & G",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.38085220m,
                Longitude = -6.24520190m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("13cdaf89-52b9-4179-a216-fa6daa6f0b0b"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE024",
                SiteName = "Balconies / Tanks Pembroke Quarter 1B",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33951900m,
                Longitude = -6.22216600m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("eba51de5-4ff4-4ed2-9d39-74e18696807f"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE025",
                SiteName = "Ballycastle Leisure Centre",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 55.20271990m,
                Longitude = -6.24640790m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("50bdfec2-fcff-42ec-8083-c5c5e3f46d0d"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE026",
                SiteName = "Bective House Hotel",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.59536780m,
                Longitude = -6.69260920m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("d08ea602-c0f4-4e1e-8bca-de331467b1dd"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE027",
                SiteName = "Belfast Children's Hospital",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 54.59366620m,
                Longitude = -5.95452870m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("7434a27e-965b-4c05-820f-694fe0cf2c82"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE028",
                SiteName = "Bolton Street",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.35236970m,
                Longitude = -6.26844640m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("566720ba-1535-4fa1-9011-930d5fd92ccf"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE029",
                SiteName = "Brownsbarn , Citywest",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.28416640m,
                Longitude = -6.42162240m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("f411aab1-257e-4634-b243-9d9abf6381e2"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE030",
                SiteName = "Bucket Clock In - Out",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.35117810m,
                Longitude = -6.26096900m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("2d93836a-99c9-4d64-8b1f-57cade9818c2"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE031",
                SiteName = "Burlington Road Project Elliotts",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33284720m,
                Longitude = -6.24633530m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("67b1dbdc-4782-4ba4-aab3-d1bfc2d26e38"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE032",
                SiteName = "Cairn Homes Blessington Demesne (PVC)",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.16965790m,
                Longitude = -6.53406870m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("d54994b4-f4e2-4f52-903d-792b5ff1022e"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE033",
                SiteName = "Cameron View - Cork Street",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33675040m,
                Longitude = -6.28800440m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("ba2a0f6f-b92f-4889-a549-94b272601465"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE034",
                SiteName = "Capel Street Hotel",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34984730m,
                Longitude = -6.26807880m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("f4f549a0-c53f-4fce-b6ac-e0829d37820d"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE035",
                SiteName = "Castleforbes Main Contract",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34892830m,
                Longitude = -6.23178520m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("f3f9ab7c-95c5-4c84-87f1-2288e435b866"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE036",
                SiteName = "Castletreasure Phase 3,",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 51.85722610m,
                Longitude = -8.43945750m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("66e9a5dd-29db-427a-8e15-50eeebc6c9a9"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE037",
                SiteName = "Cavity Drain for Ardfallen for Houses A-D",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.27449380m,
                Longitude = -6.10637250m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("b466b4ab-4864-4cdf-8f03-b687ec68ee3f"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE038",
                SiteName = "Central Plaza - Dame Street",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34466370m,
                Longitude = -6.26294310m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("f2bd53c7-8f60-4015-83ef-30c4de93db39"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE039",
                SiteName = "Cherry Orchard Point",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33402600m,
                Longitude = -6.37864200m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("02970013-904c-45f4-a3c1-d26782babbf3"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE040",
                SiteName = "Cherrywood Apartments T2 - MANNING",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.24882690m,
                Longitude = -6.15796860m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("b278388d-9c48-4327-97a5-dfcd81c987d2"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE041",
                SiteName = "Cherrywood TC4",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.24305400m,
                Longitude = -6.14330800m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("e986b115-df91-45cd-95fc-0d3d7985e39f"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE042",
                SiteName = "Churchtown Road Lower",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.29771380m,
                Longitude = -6.25484170m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("993e84d9-89b3-412c-982e-1a20c65caaf5"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE043",
                SiteName = "Clayfarm E7 Remedials",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.25503530m,
                Longitude = -6.20162150m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("4630ce3d-881e-460b-a7c7-ea62169849b4"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE044",
                SiteName = "Clay Farm Phase 2 - Blocks W1-W5",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.26647820m,
                Longitude = -6.19245100m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("92d53002-dcf0-49ad-93a1-d71137fbfc63"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE045",
                SiteName = "Clonburris Tile 6",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.32554440m,
                Longitude = -6.40619570m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("9afdf153-4309-4dfc-ad44-93ec1fe972ca"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE046",
                SiteName = "Clongriffin Blocks 5 & 6",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.40576870m,
                Longitude = -6.15139220m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("8af1eea3-39a1-417a-9286-914d0ed050f3"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE047",
                SiteName = "Cois Costa Apartments, Salthill , Galway",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.26094990m,
                Longitude = -9.10506860m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("557a5c46-58f1-447c-89e9-266b11d26df2"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE048",
                SiteName = "Coleraine Grammer School Project.",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 55.13375590m,
                Longitude = -6.68033000m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("61a6f286-c384-4979-99b1-19540c84ae54"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE049",
                SiteName = "College Square Remedial",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34613900m,
                Longitude = -6.25638820m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("8108a025-f210-4d2b-a06a-3abbfac0cf32"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE050",
                SiteName = "Coolevally - Shankhill",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.24101910m,
                Longitude = -6.11912560m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("f560d84d-1397-4aa1-8027-a7ae62174a40"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE051",
                SiteName = "Corballis Donabate",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.47984690m,
                Longitude = -6.14436830m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("4b861efd-6d39-4101-b8a9-b983c5d15e75"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE052",
                SiteName = "Cross Avenue",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.30206740m,
                Longitude = -6.19342340m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("254312b2-3516-495e-a22c-e9fdeada6264"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE053",
                SiteName = "Crown Square",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.29295130m,
                Longitude = -9.01809970m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("aaa95652-2855-467b-81e0-045d55db8bc5"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE054",
                SiteName = "Daneswell Place , Glasnevin",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.36706380m,
                Longitude = -6.26876310m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("2b81d067-9864-4a1d-854c-c11fdcb31918"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE055",
                SiteName = "Daneswell Place Sprinkler Tank",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.36709810m,
                Longitude = -6.26891340m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("14b5a206-9cc8-4e00-a1d2-ddd724c3310e"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE056",
                SiteName = "Davitt Road",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33457330m,
                Longitude = -6.31165280m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("99ae6639-072f-4c3f-bbe1-0a40d6933203"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE057",
                SiteName = "Debenhams, O'Connell Street, Limerick",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 52.66407470m,
                Longitude = -8.62698180m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("b312a1a2-9984-4a2e-a980-2090f56ad293"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE058",
                SiteName = "De La Salle , Ballyfermot",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34249190m,
                Longitude = -6.34593500m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("ea7de663-c8dd-4a57-bd5f-c95dba0821df"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE059",
                SiteName = "Development Togher More - Roundwood",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.06413870m,
                Longitude = -6.22441370m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("bd71ae45-5a26-4e09-85a3-1c27e09f0a8a"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE060",
                SiteName = "Doyles Nursery Site",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.25631200m,
                Longitude = -6.16478920m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("4b3b5db5-85b7-4c25-9a1f-c55563da2ada"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE061",
                SiteName = "DUB 10 - Building 11 (Original Project ) (Elliotts)",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33119400m,
                Longitude = -6.38893540m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("9257fbb9-8eae-4cc6-a856-480e53eb06ee"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE062",
                SiteName = "Dub 10- Building 11 (Walls)",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33106570m,
                Longitude = -6.39051750m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("9c574228-4080-4bbe-a9ac-f66089af9787"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE063",
                SiteName = "Dub 10- Building 12 -ENERGY CENTRE (Elliotts)",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33106570m,
                Longitude = -6.39051750m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("bcc7c3e8-d9ac-4045-b34a-f17606bdd1c3"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE064",
                SiteName = "Dub 10- Building 14 Elliotts",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33106570m,
                Longitude = -6.39051750m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("30102188-0714-44c4-a6db-8a44d7a53bc8"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE065",
                SiteName = "DUB 10 - Fuel Tank Zone 3 ELLIOTTS",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33106570m,
                Longitude = -6.39051750m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("8535408f-e228-44ea-9785-9bc9efb93352"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE066",
                SiteName = "Dundonald International Ice Bowl",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 54.58727720m,
                Longitude = -5.81701220m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("6e9c387b-6af5-4d6a-a819-4edd1515bac0"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE067",
                SiteName = "Eden Blackrock - Cork City",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 51.89385980m,
                Longitude = -8.41481110m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("c4c3a173-6074-43c8-b6be-e5ed77abe69f"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE068",
                SiteName = "Eden Cork Podium",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 51.89392570m,
                Longitude = -8.41213950m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("b7ecaafe-5ecf-48d6-85e5-1c582013fa3a"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE069",
                SiteName = "ESB North Wall Bunding Tanks",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.35080190m,
                Longitude = -6.21901300m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("8c550c02-8c11-42a8-8ead-fc0b60879579"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE070",
                SiteName = "Fermanagh Lakeland Forum",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 54.34277960m,
                Longitude = -7.64299990m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("5e429715-0522-427a-99e9-e5397bcae757"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE071",
                SiteName = "Firhouse Development",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.28642490m,
                Longitude = -6.33250460m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("0fed828d-8796-4d79-8215-a73484188e37"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE072",
                SiteName = "Forestside Shopping Centre",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 54.56346230m,
                Longitude = -5.90890480m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("b3973279-80ab-4635-a15d-8784bdf68b95"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE073",
                SiteName = "Fosters Avenue",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.30181700m,
                Longitude = -6.21133350m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("598f93cf-4d1e-45f5-9150-5749b0dd50dd"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE074",
                SiteName = "Galmoy Mines",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 52.79256920m,
                Longitude = -7.56824420m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("9412598b-d1f0-4c24-a784-bb164044bd18"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE075",
                SiteName = "Glenamuck Road",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.25055200m,
                Longitude = -6.18174000m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("209093f7-01b4-4a73-9f50-ba204c5873b9"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE076",
                SiteName = "Glenamuck Road, Smith Groundworks",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.24546110m,
                Longitude = -6.18619970m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("fbffd33f-c66d-47ac-8647-1daddd922005"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE077",
                SiteName = "Glencairn Balconies",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.26437220m,
                Longitude = -6.20849260m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("fc22b6b2-7e0d-4a37-9daa-2c5b821b3702"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE078",
                SiteName = "Glen Road , Development at Druids Glen",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.09188000m,
                Longitude = -6.08074000m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("89840684-5d56-43c9-9e5d-44be6d734dee"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE079",
                SiteName = "Grangegorman Student Accomodation Central Quad",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.35657700m,
                Longitude = -6.27892500m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("472589f5-7fc7-4769-b7e4-c55c61a7d40a"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE080",
                SiteName = "Hansfield Residential Block A",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.40229320m,
                Longitude = -6.42151480m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("1a683d9b-a454-41f7-aca9-5948c44cb7e2"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE081",
                SiteName = "Harcourt Square Development",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33516480m,
                Longitude = -6.26327480m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("45248d8d-1b0b-4ebd-b1a3-5f2ed96e29ce"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE082",
                SiteName = "Hostel at 6-12 Sackville Place",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34912470m,
                Longitude = -6.25812100m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("4a14899c-10f8-4424-931e-2bc9d9988c32"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE083",
                SiteName = "Jacobs Island, Cork, Basement Block 7,8,9",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 51.88210820m,
                Longitude = -8.39628320m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("5a07ded1-2efa-43d7-a47e-55edd2881fbc"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE084",
                SiteName = "Kainos Headquarters - Dublin Rd - Belfast",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 54.59205090m,
                Longitude = -5.93191480m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("d6456ca3-3b07-47ca-81a3-a59dc75df9e3"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE085",
                SiteName = "Kildare Street (JPC)",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33935140m,
                Longitude = -6.25620460m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("49b7140b-ab8e-4f3e-8248-84e6d144407e"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE086",
                SiteName = "KPH - Cherrywood Block F1 & F2",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.24066400m,
                Longitude = -6.13953350m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("a55f1ae9-cbff-4376-870c-305feab028af"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE087",
                SiteName = "Kylemore, Church Road , Killiney",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.25962880m,
                Longitude = -6.12889770m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("64cfceac-0bbe-4894-9923-38cea39a2105"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE088",
                SiteName = "Leeson Lane",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33612730m,
                Longitude = -6.25580400m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("7358f90a-027f-4ff9-8c68-eda9de80b53f"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE089",
                SiteName = "Leixlip South Lands",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.36400080m,
                Longitude = -6.52602280m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("cda37d94-9362-4606-aefd-07cd4170c04d"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE090",
                SiteName = "Lisieux Hall",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.26708080m,
                Longitude = -6.21004860m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("cce4fbbf-2315-41bd-9fd7-d13be9035f67"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE091",
                SiteName = "Lord Mayors Pub, Swords",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.45754440m,
                Longitude = -6.22088210m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("b577c504-d294-4fd2-8bb7-51912e8413d6"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE092",
                SiteName = "Lusk Block E - LC142",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.52689080m,
                Longitude = -6.18197390m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("13b4cf16-8a32-4c14-b547-1a938b8512a9"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE093",
                SiteName = "Main Street - Newtownmountkennedy",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.13576640m,
                Longitude = -6.06691770m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("b0f8b35b-3e7d-48cd-89a7-e0990e2891e1"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE094",
                SiteName = "Marmalade Lane - Wyckham Way - Dundrum",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.28533720m,
                Longitude = -6.24579420m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("9057c95c-099a-4ec5-86bc-8a5bd783980a"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE095",
                SiteName = "Martins Terrace, Hanover St. Grand Canal Dock",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34430480m,
                Longitude = -6.24114990m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("33d4a508-b8db-435f-8e24-28bdfd37c44f"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE096",
                SiteName = "McDermott Basement - Summerhill",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.45538250m,
                Longitude = -6.72646790m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("83e908ce-f505-4ca2-97d7-37da2f19c8de"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE097",
                SiteName = "Merville Place - Finglas - Dublin",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.38308380m,
                Longitude = -6.29202750m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("eb1611c8-4918-4090-8cf8-17d6d2c601de"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE098",
                SiteName = "Montrose, Donnybrook,",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.32937000m,
                Longitude = -6.22750490m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("0061f0f0-d19a-4f2f-8178-b38ea107ddf9"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE099",
                SiteName = "Mount Prospect Ave, Clontarf",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 42.03231550m,
                Longitude = -87.92102830m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("9447025c-4831-47bd-a973-530ab0d93135"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE100",
                SiteName = "Murphystown Way",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.26647820m,
                Longitude = -6.19245100m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("6aa75ad2-444d-4962-bbf7-24553531dd17"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE101",
                SiteName = "NCH (National Children Hospital)",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33780180m,
                Longitude = -6.29645260m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("b2f405bf-d5d5-410e-8633-ad89403e282e"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE102",
                SiteName = "Newcastle Creche Lift Pit",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.29741270m,
                Longitude = -6.49388970m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("defb6a98-2298-4f1c-a439-6d53234676df"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE103",
                SiteName = "Niven Oaks - Kwik (RADON BARRIER)",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.40280510m,
                Longitude = -6.26016850m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("e361ef4b-b15d-4753-ae67-531a8dfc87aa"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE104",
                SiteName = "Niven Oaks, Northwood",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.40298190m,
                Longitude = -6.26022750m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("8b5dab5a-62a2-4330-bc10-c6cac1915d98"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE105",
                SiteName = "No 2 Grande Parade",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.32346870m,
                Longitude = -6.26031450m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("3fd92cd7-3a0f-4f4a-a3b5-7d2d23d09971"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE106",
                SiteName = "Northern Cross Block 5",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.39735940m,
                Longitude = -6.18707200m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("f5d6d0a3-1b00-492d-8e1a-bdd28bb08bcd"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE107",
                SiteName = "O Devaney Gardens",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.35198480m,
                Longitude = -6.29458910m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("df014557-6730-4bad-9b34-12c3d0ce0287"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE108",
                SiteName = "Office/Warehouse Gorey",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 52.69283770m,
                Longitude = -6.27189080m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("e3808226-c0ef-4401-ace5-96d2c9b78b8f"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE109",
                SiteName = "Pembroke Quarter",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33974200m,
                Longitude = -6.21576000m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("b7440046-2f74-4f35-a4b4-db672e7d7923"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE110",
                SiteName = "Pipers Square",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.40444230m,
                Longitude = -6.30318000m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("fd6c7d89-6593-4f5e-b1e9-fb04bcad0294"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE111",
                SiteName = "Podium at Airton Road",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.29367780m,
                Longitude = -6.36286920m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("869ebeef-a1c4-432d-b1d7-84153ba99366"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE112",
                SiteName = "Prembroke Quarter - Phase 1B",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33834670m,
                Longitude = -6.20674430m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("6e978cd8-9a0c-44b0-9193-4686a54e0980"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE113",
                SiteName = "Premier Inn Clerys, Dublin",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34916320m,
                Longitude = -6.25871320m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("a4639073-ab7a-47e3-b0c1-9df05fa74168"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE114",
                SiteName = "Premier Inn Hotel, Castleforbes, Montane",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.35014010m,
                Longitude = -6.23375670m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("0abb032a-d601-4246-ac35-befa19cbebc7"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE115",
                SiteName = "Profile Park Grangecastle",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.32160160m,
                Longitude = -6.40940030m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("bda52f08-7c7a-42c4-a585-359ce7ec0933"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE116",
                SiteName = "Pure Data Centre DUB01",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.40669010m,
                Longitude = -6.35818770m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("1daa745c-5877-4b31-9c99-66c49abe5e0d"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE117",
                SiteName = "Radon Works at Rockbrook Project , Sandyford",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.27765620m,
                Longitude = -6.21249180m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("64fcd16a-12c3-4ce1-896d-0d8929f712ce"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE118",
                SiteName = "QuantumBuild Ireland Office Gorey",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 52.69197200m,
                Longitude = -6.27006260m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("3b927a3e-0c99-4907-8cc5-38f9886369b0"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE119",
                SiteName = "Rathborne Crossing",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.37721970m,
                Longitude = -6.32566370m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("455e6177-3971-4967-b66d-40f97b849f35"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE120",
                SiteName = "Ringsend Hybrid",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34004770m,
                Longitude = -6.18779620m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("a7da4072-ec10-4953-b4f9-2e75fc64fa15"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE121",
                SiteName = "Rockbrook external walkways , Block A & B",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.27765620m,
                Longitude = -6.21249180m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("5caad954-5231-480b-b7de-65582f65495a"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE122",
                SiteName = "Rockbrook Residential Developments Stillorgan",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.27641900m,
                Longitude = -6.20895980m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("bc8fa175-880c-4c20-bb35-b79a62c3e26a"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE123",
                SiteName = "Rutland Street School Development",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.35484640m,
                Longitude = -6.25213390m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("f97c83e6-b4c8-477c-bc68-c143e83ebc3a"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE124",
                SiteName = "Sandbox Test Project",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.21392210m,
                Longitude = -6.69793980m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("c710861c-6554-4a66-bf30-0db489e7a938"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE125",
                SiteName = "Sandymount Place, Sandymount Avenue.",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.32828350m,
                Longitude = -6.21988240m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("aa640856-b94f-4eb7-bd46-ec0f51c46ed4"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE126",
                SiteName = "Sanofi, Waterford",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 52.24813610m,
                Longitude = -7.17520230m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("28933e5e-6719-4fda-903a-d0f75a15180b"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE127",
                SiteName = "Season Park , DRES , Newtownmountkennedy",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.09469650m,
                Longitude = -6.11584360m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("705b215e-f3cc-4b6d-8ce6-bec69131082c"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE128",
                SiteName = "South West Gate , Naas Road",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.32140820m,
                Longitude = -6.33955280m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("c2fa21d7-3c31-4ea7-8034-8aaf57470333"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE129",
                SiteName = "Stemple Stadium , Blanchardstown",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.41323110m,
                Longitude = -6.36617840m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("9072b579-5f7f-41ef-8446-cba561e3d1f0"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE130",
                SiteName = "St James Apartments , James Street , Dublin",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34217250m,
                Longitude = -6.29028970m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("dd74da65-6695-4599-bf7a-d44d0a025cc3"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE131",
                SiteName = "Student Accommodation - Leinster St. - Maynooth",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.38217700m,
                Longitude = -6.58950130m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("ddc61b36-5f7f-427c-bbe1-63121aec3845"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE132",
                SiteName = "SWF Oscar Traynor Road",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.39332180m,
                Longitude = -6.20924890m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("e708676d-386e-4cb0-b838-fbe9c68d05b6"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE133",
                SiteName = "TC1 Balconies at Cherrywood - Conack",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.24362490m,
                Longitude = -6.14754950m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("1d5b82fa-53e7-4d4b-a3a9-b16824b953bd"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE134",
                SiteName = "The Picture House - Phibsborough",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.36034850m,
                Longitude = -6.27250490m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("611c6cd5-0a77-44ab-8f10-7360ba180a5c"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE135",
                SiteName = "The Verde PBSA, Dublin Road, Belfast",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 54.59169740m,
                Longitude = -5.93237060m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("f8d29dfb-246a-41f9-86b0-4860222d39ef"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE136",
                SiteName = "Tile 4&5 - Clonburris",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33146620m,
                Longitude = -6.40735380m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("6a261eba-c4f2-49f3-b7c5-44e4ce8879ab"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE137",
                SiteName = "Tile 6, Block B, C, E, & F",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.33033100m,
                Longitude = -6.41356840m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("31b7516e-79b6-4cbc-9de7-93e40e243e8d"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE138",
                SiteName = "Turvey Avenue",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34122510m,
                Longitude = -6.31278320m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("56c24d2b-8f5e-4aa4-b3bb-48540d481192"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE139",
                SiteName = "Twilfit House, Upr. Abbey St.",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34735780m,
                Longitude = -6.26695800m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("149e1703-7bd0-44ef-ba5a-2e75d1eb42a5"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE140",
                SiteName = "UCD Science Centre, Phase 3",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.30972680m,
                Longitude = -6.22158970m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("c0ad0c56-e4ba-4a9f-a028-a1dc42d94286"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE141",
                SiteName = "Vardis Group - Pool Leaks",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.35661370m,
                Longitude = -6.37148080m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("768c9f88-dbc7-4a31-9175-b2b6a1cefe11"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE142",
                SiteName = "Victoria Cross Road, Cork",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 51.89133720m,
                Longitude = -8.50638010m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("49bb0000-1f58-4baf-b35e-e46d44c93ed4"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE143",
                SiteName = "Walkinstown Podium Apt Development",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.31690670m,
                Longitude = -6.33932850m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("8a12bcae-adce-4960-98ab-5f977b158469"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE144",
                SiteName = "Whitehaven SHD",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.40293220m,
                Longitude = -6.24983650m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("681db970-4236-4967-bece-f91446ff9393"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE145",
                SiteName = "Woodbrook Block A B & C, Dublin",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.23367090m,
                Longitude = -6.12265690m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("6a4224b1-3fb9-4521-93ee-1d7ab7bc4f06"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE146",
                SiteName = "Woodward Court , Glencairn Gate - Park Developments",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.26545040m,
                Longitude = -6.20383690m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("f93c5cf5-7d62-478e-9a6e-e790b97e747f"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE147",
                SiteName = "Francis Street Apart Hotel",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.34230800m,
                Longitude = -6.27587850m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("57f30247-24c4-4fc0-8ce4-12c0c0d18ca4"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE148",
                SiteName = "Podium @ Oscar Traynor Road",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = 53.39818160m,
                Longitude = -6.22519360m,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("71e98eae-1b1a-4836-a87f-0cc910616683"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE149",
                SiteName = "Cabra Window Spraying",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("e305aab3-7883-4405-86fe-38841b5c3a9c"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE150",
                SiteName = "Seatown Road Social Housing",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("34f61685-d5e9-44e8-b6af-21f51fbf9798"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE151",
                SiteName = "Clonburris Seven Mills Tile 2",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("03b5dba2-cb06-4b98-a25d-f8f63bbb5200"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE152",
                SiteName = "Four Park Place",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("8291f65c-e963-4892-b30f-46edf3d57379"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE153",
                SiteName = "Cherrywood Apts, Dublin -CAIRN HOMES NEW PROJECT",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("bf5d5613-d2f3-433e-9368-48b5dcc7df0c"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE154",
                SiteName = "Station Road , Raheny",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("e937e577-3487-4323-a612-947d01571313"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE155",
                SiteName = "Claremont Development",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("d0f88d3e-5770-4b40-ab95-fa4d4a69e9e2"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE156",
                SiteName = "Waterford Crystal Campus",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("52e30f0a-99cd-4e1e-80c7-129987164555"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE157",
                SiteName = "T5 Cherrywood Basement & Podium, Blocks A-D",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("34d6d1b3-6c12-4071-a5c0-a13c25467d5c"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE158",
                SiteName = "Celbridge",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("65f71b08-b10b-4293-a5c3-7418f3ffdf04"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE159",
                SiteName = "Saggart Resevoir Remedials",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("c1635c37-d55d-4b65-b5bb-0ba172e2d49a"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE160",
                SiteName = "Cherrywood TC2 Balconies",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("74c6f5c2-8776-4fda-9182-995d39546356"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE161",
                SiteName = "Glenveagh Newtownpark Avenue , Blackrock",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("287bc71d-0751-4087-9071-90060d0777c4"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE162",
                SiteName = "Navan East Phase 3",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("8b1ed6e4-1270-4554-8431-722360505006"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE163",
                SiteName = "Baroda 110KV Substation, Kildare - PFIZER IRELAND",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("da74c851-29a3-4c34-8366-f6af227054b6"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE164",
                SiteName = "Monkstown Hall, Dun Laoghaire",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("86cde3d6-db3d-469d-8d27-3791f76c08a7"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE165",
                SiteName = "Shanaganagh Castle",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("ec1cf9e6-335a-44f3-8cb7-95edbd75e827"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE166",
                SiteName = "Residential Development at Ratoath",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("58a24c5f-5a80-4b1b-9a03-603949fdd227"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE167",
                SiteName = "Barnhill Development",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("76046b3d-128c-4133-b0d1-79341492652f"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE168",
                SiteName = "Leisure Plex Development",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("b53ca94b-fa17-4321-8287-38b0977e91d2"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE169",
                SiteName = "Strand Road, Bray (McEleney Homes)",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("2ca20c8d-3739-465f-9ce7-0d5031a94a67"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE170",
                SiteName = "UCD Phase 2A , Village A1 -SISK",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("9042d05e-57ee-4ae2-b40c-a2b701bb1c01"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE171",
                SiteName = "Phoenix Park Apartment Block 3",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("43831f49-7f3c-4471-9f11-c6ae39ab5ffc"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE172",
                SiteName = "Cherry Orchard Point",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            },
            new Site
            {
                Id = Guid.Parse("c847aaa0-5792-477d-b8c8-73d060ad7be3"),
                TenantId = DefaultTenantId,
                SiteCode = "SITE173",
                SiteName = "Airton Road, Blocks E-F",
                IsActive = true,
                Notes = "Site Type: Project Site",
                GeofenceRadiusMeters = 100,
                Latitude = null,
                Longitude = null,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            }
        };

        await sites.AddRangeAsync(sitesToCreate);
        await context.SaveChangesAsync();
        logger.LogInformation("Created {Count} test sites", sitesToCreate.Count);
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
            Permissions.ToolboxTalks.View => "View toolbox talks",
            Permissions.ToolboxTalks.Create => "Create toolbox talks",
            Permissions.ToolboxTalks.Edit => "Edit toolbox talks",
            Permissions.ToolboxTalks.Delete => "Delete toolbox talks",
            Permissions.ToolboxTalks.Schedule => "Schedule toolbox talks",
            Permissions.ToolboxTalks.ViewReports => "View toolbox talks reports",
            Permissions.ToolboxTalks.Admin => "Full toolbox talks administration",

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
}
