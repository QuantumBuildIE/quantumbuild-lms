using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Text.Json;

namespace QuantumBuild.Tests.Common.TestTenant;

/// <summary>
/// Seeds comprehensive test data for the automated test tenant.
/// Creates data in isolation from the default QUANTUMBUILD tenant.
/// </summary>
public class TestTenantSeeder
{
    private readonly DbContext _context;
    private readonly ILogger _logger;
    private readonly IServiceProvider? _serviceProvider;

    public TestTenantSeeder(DbContext context, ILogger logger)
    {
        _context = context;
        _logger = logger;
        _serviceProvider = null;
    }

    /// <summary>
    /// Creates a TestTenantSeeder with access to UserManager for proper password hashing.
    /// </summary>
    public TestTenantSeeder(DbContext context, ILogger logger, IServiceProvider serviceProvider)
    {
        _context = context;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Seeds all test tenant data across all modules.
    /// </summary>
    public async Task SeedAllAsync()
    {
        await SeedCoreAsync();
        await SeedToolboxTalksAsync();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Test tenant seeding completed successfully");
    }

    /// <summary>
    /// Resets test tenant data by cleaning and re-seeding.
    /// </summary>
    public async Task ResetAsync()
    {
        await CleanupAsync();
        await SeedAllAsync();
    }

    /// <summary>
    /// Cleans up all test tenant data in FK-safe order.
    /// </summary>
    public async Task CleanupAsync()
    {
        var tenantId = TestTenantConstants.TenantId;

        // Delete in FK-safe order (children first)

        // ToolboxTalks module (deepest children first)
        await ExecuteDeleteAsync("ScheduledTalkCompletions", "\"ScheduledTalkId\" IN (SELECT \"Id\" FROM \"ScheduledTalks\" WHERE \"TenantId\" = {0})", tenantId);
        await ExecuteDeleteAsync("ScheduledTalkSectionProgress", "\"ScheduledTalkId\" IN (SELECT \"Id\" FROM \"ScheduledTalks\" WHERE \"TenantId\" = {0})", tenantId);
        await ExecuteDeleteAsync("ScheduledTalkQuizAttempts", "\"ScheduledTalkId\" IN (SELECT \"Id\" FROM \"ScheduledTalks\" WHERE \"TenantId\" = {0})", tenantId);
        await ExecuteDeleteAsync("ScheduledTalks", "\"TenantId\" = {0}", tenantId);
        await ExecuteDeleteAsync("ToolboxTalkScheduleAssignments", "\"ScheduleId\" IN (SELECT \"Id\" FROM \"ToolboxTalkSchedules\" WHERE \"TenantId\" = {0})", tenantId);
        await ExecuteDeleteAsync("ToolboxTalkSchedules", "\"TenantId\" = {0}", tenantId);
        await ExecuteDeleteAsync("ToolboxTalkQuestions", "\"ToolboxTalkId\" IN (SELECT \"Id\" FROM \"ToolboxTalks\" WHERE \"TenantId\" = {0})", tenantId);
        await ExecuteDeleteAsync("ToolboxTalkSections", "\"ToolboxTalkId\" IN (SELECT \"Id\" FROM \"ToolboxTalks\" WHERE \"TenantId\" = {0})", tenantId);
        await ExecuteDeleteAsync("ToolboxTalks", "\"TenantId\" = {0}", tenantId);
        await ExecuteDeleteAsync("ToolboxTalkSettings", "\"TenantId\" = {0}", tenantId);

        // Core module (must be last due to foreign keys)
        await ExecuteDeleteAsync("Contacts", "\"TenantId\" = {0}", tenantId);
        await ExecuteDeleteAsync("Companies", "\"TenantId\" = {0}", tenantId);
        await ExecuteDeleteAsync("Employees", "\"TenantId\" = {0}", tenantId);
        await ExecuteDeleteAsync("Sites", "\"TenantId\" = {0}", tenantId);

        _logger.LogInformation("Test tenant data cleanup completed");
    }

    private async Task ExecuteDeleteAsync(string tableName, string whereClause, params object[] parameters)
    {
        try
        {
            var sql = $"DELETE FROM \"{tableName}\" WHERE {whereClause}";
            await _context.Database.ExecuteSqlRawAsync(sql, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Delete from {Table} skipped: {Message}", tableName, ex.Message);
        }
    }

    #region Core Module Seeding

    private async Task SeedCoreAsync()
    {
        await SeedTenantAsync();
        await SeedUsersAsync();
        await SeedSitesAsync();
        await SeedEmployeesAsync();
        await SeedCompaniesAsync();
        await SeedContactsAsync();
    }

    private async Task SeedTenantAsync()
    {
        var tenants = _context.Set<Tenant>();

        if (await tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == TestTenantConstants.TenantId))
        {
            _logger.LogInformation("Test tenant already exists, skipping");
            return;
        }

        var tenant = new Tenant
        {
            Id = TestTenantConstants.TenantId,
            Name = TestTenantConstants.TenantName,
            Code = TestTenantConstants.TenantCode,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-seeder"
        };

        await tenants.AddAsync(tenant);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created test tenant: {TenantName}", tenant.Name);
    }

    private async Task SeedUsersAsync()
    {
        // If we have a service provider, use UserManager for proper password hashing
        if (_serviceProvider != null)
        {
            await SeedUsersWithUserManagerAsync();
            return;
        }

        // Fallback to direct DB insert (users won't be able to login via password)
        var users = _context.Set<User>();

        if (await users.IgnoreQueryFilters().AnyAsync(u => u.TenantId == TestTenantConstants.TenantId))
        {
            _logger.LogInformation("Test users already exist, skipping");
            return;
        }

        var usersToCreate = new List<User>
        {
            new User
            {
                Id = TestTenantConstants.Users.Admin.Id,
                TenantId = TestTenantConstants.TenantId,
                Email = TestTenantConstants.Users.Admin.Email,
                NormalizedEmail = TestTenantConstants.Users.Admin.Email.ToUpperInvariant(),
                UserName = TestTenantConstants.Users.Admin.Email,
                NormalizedUserName = TestTenantConstants.Users.Admin.Email.ToUpperInvariant(),
                FirstName = TestTenantConstants.Users.Admin.FirstName,
                LastName = TestTenantConstants.Users.Admin.LastName,
                EmailConfirmed = true,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new User
            {
                Id = TestTenantConstants.Users.SiteManager.Id,
                TenantId = TestTenantConstants.TenantId,
                Email = TestTenantConstants.Users.SiteManager.Email,
                NormalizedEmail = TestTenantConstants.Users.SiteManager.Email.ToUpperInvariant(),
                UserName = TestTenantConstants.Users.SiteManager.Email,
                NormalizedUserName = TestTenantConstants.Users.SiteManager.Email.ToUpperInvariant(),
                FirstName = TestTenantConstants.Users.SiteManager.FirstName,
                LastName = TestTenantConstants.Users.SiteManager.LastName,
                EmailConfirmed = true,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new User
            {
                Id = TestTenantConstants.Users.Warehouse.Id,
                TenantId = TestTenantConstants.TenantId,
                Email = TestTenantConstants.Users.Warehouse.Email,
                NormalizedEmail = TestTenantConstants.Users.Warehouse.Email.ToUpperInvariant(),
                UserName = TestTenantConstants.Users.Warehouse.Email,
                NormalizedUserName = TestTenantConstants.Users.Warehouse.Email.ToUpperInvariant(),
                FirstName = TestTenantConstants.Users.Warehouse.FirstName,
                LastName = TestTenantConstants.Users.Warehouse.LastName,
                EmailConfirmed = true,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new User
            {
                Id = TestTenantConstants.Users.Operator.Id,
                TenantId = TestTenantConstants.TenantId,
                Email = TestTenantConstants.Users.Operator.Email,
                NormalizedEmail = TestTenantConstants.Users.Operator.Email.ToUpperInvariant(),
                UserName = TestTenantConstants.Users.Operator.Email,
                NormalizedUserName = TestTenantConstants.Users.Operator.Email.ToUpperInvariant(),
                FirstName = TestTenantConstants.Users.Operator.FirstName,
                LastName = TestTenantConstants.Users.Operator.LastName,
                EmailConfirmed = true,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new User
            {
                Id = TestTenantConstants.Users.Finance.Id,
                TenantId = TestTenantConstants.TenantId,
                Email = TestTenantConstants.Users.Finance.Email,
                NormalizedEmail = TestTenantConstants.Users.Finance.Email.ToUpperInvariant(),
                UserName = TestTenantConstants.Users.Finance.Email,
                NormalizedUserName = TestTenantConstants.Users.Finance.Email.ToUpperInvariant(),
                FirstName = TestTenantConstants.Users.Finance.FirstName,
                LastName = TestTenantConstants.Users.Finance.LastName,
                EmailConfirmed = true,
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            }
        };

        await users.AddRangeAsync(usersToCreate);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created {Count} test users (without password hashes - login via password will not work)", usersToCreate.Count);
    }

    /// <summary>
    /// Seeds users using UserManager for proper password hashing.
    /// This allows users to login via the actual login endpoint.
    /// </summary>
    private async Task SeedUsersWithUserManagerAsync()
    {
        var userManager = _serviceProvider!.GetRequiredService<UserManager<User>>();
        var roleManager = _serviceProvider.GetRequiredService<RoleManager<Role>>();

        // Define users to create with their credentials and roles
        var usersToCreate = new (Guid Id, string Email, string Password, string FirstName, string LastName, string Role)[]
        {
            (TestTenantConstants.Users.Admin.Id, TestTenantConstants.Users.Admin.Email, TestTenantConstants.Users.Admin.Password, TestTenantConstants.Users.Admin.FirstName, TestTenantConstants.Users.Admin.LastName, "Admin"),
            (TestTenantConstants.Users.SiteManager.Id, TestTenantConstants.Users.SiteManager.Email, TestTenantConstants.Users.SiteManager.Password, TestTenantConstants.Users.SiteManager.FirstName, TestTenantConstants.Users.SiteManager.LastName, "SiteManager"),
            (TestTenantConstants.Users.Warehouse.Id, TestTenantConstants.Users.Warehouse.Email, TestTenantConstants.Users.Warehouse.Password, TestTenantConstants.Users.Warehouse.FirstName, TestTenantConstants.Users.Warehouse.LastName, "WarehouseStaff"),
            (TestTenantConstants.Users.Operator.Id, TestTenantConstants.Users.Operator.Email, TestTenantConstants.Users.Operator.Password, TestTenantConstants.Users.Operator.FirstName, TestTenantConstants.Users.Operator.LastName, "Operator"),
            (TestTenantConstants.Users.Finance.Id, TestTenantConstants.Users.Finance.Email, TestTenantConstants.Users.Finance.Password, TestTenantConstants.Users.Finance.FirstName, TestTenantConstants.Users.Finance.LastName, "Finance")
        };

        foreach (var userInfo in usersToCreate)
        {
            // Check if user already exists
            var existingUser = await userManager.FindByEmailAsync(userInfo.Email);
            if (existingUser != null)
            {
                _logger.LogInformation("Test user {Email} already exists, skipping", userInfo.Email);
                continue;
            }

            var user = new User
            {
                Id = userInfo.Id,
                TenantId = TestTenantConstants.TenantId,
                Email = userInfo.Email,
                UserName = userInfo.Email,
                FirstName = userInfo.FirstName,
                LastName = userInfo.LastName,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            };

            // Use UserManager to create with proper password hash
            var result = await userManager.CreateAsync(user, userInfo.Password);
            if (result.Succeeded)
            {
                _logger.LogInformation("Created test user: {Email}", userInfo.Email);

                // Assign role if it exists
                var role = await roleManager.FindByNameAsync(userInfo.Role);
                if (role != null)
                {
                    var roleResult = await userManager.AddToRoleAsync(user, userInfo.Role);
                    if (roleResult.Succeeded)
                    {
                        _logger.LogInformation("Assigned {Role} role to user: {Email}", userInfo.Role, userInfo.Email);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to assign {Role} role to {Email}: {Errors}",
                            userInfo.Role, userInfo.Email, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    _logger.LogWarning("Role {Role} not found for user {Email}", userInfo.Role, userInfo.Email);
                }
            }
            else
            {
                _logger.LogWarning("Failed to create test user {Email}: {Errors}",
                    userInfo.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private async Task SeedSitesAsync()
    {
        var sites = _context.Set<Site>();

        if (await sites.IgnoreQueryFilters().AnyAsync(s => s.TenantId == TestTenantConstants.TenantId))
        {
            _logger.LogInformation("Test sites already exist, skipping");
            return;
        }

        var sitesToCreate = new List<Site>
        {
            new Site
            {
                Id = TestTenantConstants.Sites.MainSite,
                TenantId = TestTenantConstants.TenantId,
                SiteCode = TestTenantConstants.Sites.MainSiteCode,
                SiteName = TestTenantConstants.Sites.MainSiteName,
                Address = "123 Test Street",
                City = "Dublin",
                PostalCode = "D01 TEST",
                Latitude = TestTenantConstants.Sites.MainSiteLatitude,
                Longitude = TestTenantConstants.Sites.MainSiteLongitude,
                GeofenceRadiusMeters = 100,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new Site
            {
                Id = TestTenantConstants.Sites.SecondarySite,
                TenantId = TestTenantConstants.TenantId,
                SiteCode = TestTenantConstants.Sites.SecondarySiteCode,
                SiteName = TestTenantConstants.Sites.SecondarySiteName,
                Address = "456 Secondary Road",
                City = "Cork",
                PostalCode = "T12 TEST",
                Latitude = TestTenantConstants.Sites.SecondarySiteLatitude,
                Longitude = TestTenantConstants.Sites.SecondarySiteLongitude,
                GeofenceRadiusMeters = 150,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new Site
            {
                Id = TestTenantConstants.Sites.InactiveSite,
                TenantId = TestTenantConstants.TenantId,
                SiteCode = TestTenantConstants.Sites.InactiveSiteCode,
                SiteName = TestTenantConstants.Sites.InactiveSiteName,
                Address = "789 Inactive Lane",
                City = "Galway",
                PostalCode = "H91 TEST",
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            }
        };

        await sites.AddRangeAsync(sitesToCreate);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created {Count} test sites", sitesToCreate.Count);
    }

    private async Task SeedEmployeesAsync()
    {
        var employees = _context.Set<Employee>();

        if (await employees.IgnoreQueryFilters().AnyAsync(e => e.TenantId == TestTenantConstants.TenantId))
        {
            _logger.LogInformation("Test employees already exist, skipping");
            return;
        }

        var employeesToCreate = new List<Employee>
        {
            new Employee
            {
                Id = TestTenantConstants.Employees.Employee1,
                TenantId = TestTenantConstants.TenantId,
                EmployeeCode = TestTenantConstants.Employees.Employee1Code,
                FirstName = TestTenantConstants.Employees.Employee1FirstName,
                LastName = TestTenantConstants.Employees.Employee1LastName,
                Email = "john.test@test.quantumbuild.ie",
                JobTitle = "Site Worker",
                PrimarySiteId = TestTenantConstants.Sites.MainSite,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddYears(-2),
                PreferredLanguage = "en",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new Employee
            {
                Id = TestTenantConstants.Employees.Employee2,
                TenantId = TestTenantConstants.TenantId,
                EmployeeCode = TestTenantConstants.Employees.Employee2Code,
                FirstName = TestTenantConstants.Employees.Employee2FirstName,
                LastName = TestTenantConstants.Employees.Employee2LastName,
                Email = "jane.test@test.quantumbuild.ie",
                JobTitle = "Site Worker",
                PrimarySiteId = TestTenantConstants.Sites.MainSite,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddYears(-1),
                PreferredLanguage = "en",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new Employee
            {
                Id = TestTenantConstants.Employees.ManagerEmployee,
                TenantId = TestTenantConstants.TenantId,
                EmployeeCode = TestTenantConstants.Employees.ManagerEmployeeCode,
                FirstName = TestTenantConstants.Employees.ManagerEmployeeFirstName,
                LastName = TestTenantConstants.Employees.ManagerEmployeeLastName,
                Email = "manager.test@test.quantumbuild.ie",
                JobTitle = "Site Manager",
                PrimarySiteId = TestTenantConstants.Sites.MainSite,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddYears(-5),
                PreferredLanguage = "en",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new Employee
            {
                Id = TestTenantConstants.Employees.Employee3,
                TenantId = TestTenantConstants.TenantId,
                EmployeeCode = TestTenantConstants.Employees.Employee3Code,
                FirstName = TestTenantConstants.Employees.Employee3FirstName,
                LastName = TestTenantConstants.Employees.Employee3LastName,
                Email = "bob.test@test.quantumbuild.ie",
                JobTitle = "Site Worker",
                PrimarySiteId = TestTenantConstants.Sites.MainSite,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddYears(-1),
                PreferredLanguage = "en",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new Employee
            {
                Id = TestTenantConstants.Employees.OperatorEmployee,
                TenantId = TestTenantConstants.TenantId,
                EmployeeCode = TestTenantConstants.Employees.OperatorEmployeeCode,
                FirstName = TestTenantConstants.Employees.OperatorEmployeeFirstName,
                LastName = TestTenantConstants.Employees.OperatorEmployeeLastName,
                Email = "operator.test@test.quantumbuild.ie",
                JobTitle = "Operator",
                PrimarySiteId = TestTenantConstants.Sites.MainSite,
                IsActive = true,
                StartDate = DateTime.UtcNow.AddMonths(-6),
                PreferredLanguage = "en",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            }
        };

        await employees.AddRangeAsync(employeesToCreate);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created {Count} test employees", employeesToCreate.Count);
    }

    private async Task SeedCompaniesAsync()
    {
        var companies = _context.Set<Company>();

        if (await companies.IgnoreQueryFilters().AnyAsync(c => c.TenantId == TestTenantConstants.TenantId))
        {
            _logger.LogInformation("Test companies already exist, skipping");
            return;
        }

        var companiesToCreate = new List<Company>
        {
            new Company
            {
                Id = TestTenantConstants.Companies.CustomerCompany1,
                TenantId = TestTenantConstants.TenantId,
                CompanyCode = "TC001",
                CompanyName = TestTenantConstants.Companies.CustomerCompany1Name,
                TradingName = "Test Customer",
                CompanyType = "Customer",
                AddressLine1 = "100 Customer Street",
                City = "Dublin",
                PostalCode = "D02 TEST",
                Phone = "+353 1 234 5678",
                Email = "info@testcustomer.ie",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            },
            new Company
            {
                Id = TestTenantConstants.Companies.CustomerCompany2,
                TenantId = TestTenantConstants.TenantId,
                CompanyCode = "TC002",
                CompanyName = TestTenantConstants.Companies.CustomerCompany2Name,
                TradingName = "Test Construction",
                CompanyType = "Customer",
                AddressLine1 = "200 Construction Ave",
                City = "Cork",
                PostalCode = "T12 TEST",
                Phone = "+353 21 234 5678",
                Email = "info@testconstruction.ie",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            }
        };

        await companies.AddRangeAsync(companiesToCreate);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created {Count} test companies", companiesToCreate.Count);
    }

    private async Task SeedContactsAsync()
    {
        var contacts = _context.Set<Contact>();

        if (await contacts.IgnoreQueryFilters().AnyAsync(c => c.TenantId == TestTenantConstants.TenantId))
        {
            _logger.LogInformation("Test contacts already exist, skipping");
            return;
        }

        var contactsToCreate = new List<Contact>
        {
            new Contact
            {
                Id = TestTenantConstants.Contacts.Contact1,
                TenantId = TestTenantConstants.TenantId,
                CompanyId = TestTenantConstants.Companies.CustomerCompany1,
                FirstName = TestTenantConstants.Contacts.Contact1FirstName,
                LastName = TestTenantConstants.Contacts.Contact1LastName,
                Email = TestTenantConstants.Contacts.Contact1Email,
                Phone = "+353 87 123 4567",
                JobTitle = "Project Manager",
                IsPrimaryContact = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seeder"
            }
        };

        await contacts.AddRangeAsync(contactsToCreate);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created {Count} test contacts", contactsToCreate.Count);
    }

    #endregion

    #region ToolboxTalks Module Seeding

    // Store section IDs for use in progress tracking
    private readonly Dictionary<string, Guid> _sectionIds = new();

    private async Task SeedToolboxTalksAsync()
    {
        await SeedToolboxTalkSettingsAsync();
        await SeedToolboxTalkEntitiesAsync();
        await SeedToolboxTalkSchedulesAsync();
        await SeedScheduledTalksAsync();
    }

    private async Task SeedToolboxTalkSettingsAsync()
    {
        var settings = _context.Set<ToolboxTalkSettings>();

        if (await settings.AnyAsync(s => s.TenantId == TestTenantConstants.TenantId))
        {
            _logger.LogInformation("Test toolbox talk settings already exist, skipping");
            return;
        }

        var settingsToCreate = new ToolboxTalkSettings
        {
            Id = TestTenantConstants.ToolboxTalks.Settings,
            TenantId = TestTenantConstants.TenantId,
            DefaultDueDays = 7,
            ReminderFrequencyDays = 1,
            MaxReminders = 5,
            EscalateAfterReminders = 3,
            RequireVideoCompletion = true,
            DefaultPassingScore = 80,
            EnableTranslation = false,
            EnableVideoDubbing = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-seeder"
        };

        await settings.AddAsync(settingsToCreate);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created test Learning settings");
    }

    private async Task SeedToolboxTalkEntitiesAsync()
    {
        var toolboxTalks = _context.Set<ToolboxTalk>();

        if (await toolboxTalks.IgnoreQueryFilters().AnyAsync(t => t.TenantId == TestTenantConstants.TenantId))
        {
            _logger.LogInformation("Test Learnings already exist, skipping");
            return;
        }

        // 1. Basic Talk - simple with 2 sections, no quiz
        var basicTalkSection1Id = Guid.NewGuid();
        var basicTalkSection2Id = Guid.NewGuid();
        _sectionIds["BasicTalk_Section1"] = basicTalkSection1Id;
        _sectionIds["BasicTalk_Section2"] = basicTalkSection2Id;

        var basicTalk = new ToolboxTalk
        {
            Id = TestTenantConstants.ToolboxTalks.Talks.BasicTalk,
            TenantId = TestTenantConstants.TenantId,
            Title = TestTenantConstants.ToolboxTalks.Talks.BasicTalkTitle,
            Description = "A basic safety talk covering fundamental workplace safety principles.",
            Frequency = ToolboxTalkFrequency.Once,
            VideoSource = VideoSource.None,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-seeder",
            Sections = new List<ToolboxTalkSection>
            {
                new ToolboxTalkSection
                {
                    Id = basicTalkSection1Id,
                    ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk,
                    SectionNumber = 1,
                    Title = "Introduction to Workplace Safety",
                    Content = @"<h2>Welcome to Workplace Safety</h2>
<p>Safety is everyone's responsibility. This talk covers the basic principles you need to know.</p>
<ul>
    <li>Always wear appropriate PPE</li>
    <li>Report hazards immediately</li>
    <li>Follow all posted safety signs</li>
    <li>Know your emergency exits</li>
</ul>",
                    RequiresAcknowledgment = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test-seeder"
                },
                new ToolboxTalkSection
                {
                    Id = basicTalkSection2Id,
                    ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk,
                    SectionNumber = 2,
                    Title = "Personal Protective Equipment",
                    Content = @"<h2>PPE Requirements</h2>
<p>Personal Protective Equipment (PPE) is your last line of defense against workplace hazards.</p>
<h3>Required PPE on Site:</h3>
<ul>
    <li><strong>Hard Hat</strong> - Required in all construction areas</li>
    <li><strong>Safety Boots</strong> - Steel-toed footwear mandatory</li>
    <li><strong>High-Vis Vest</strong> - Must be worn at all times on site</li>
    <li><strong>Safety Glasses</strong> - Required when operating machinery</li>
</ul>",
                    RequiresAcknowledgment = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test-seeder"
                }
            }
        };

        // 2. Talk with Quiz - 3 sections and 3 questions
        var quizTalkSection1Id = Guid.NewGuid();
        var quizTalkSection2Id = Guid.NewGuid();
        var quizTalkSection3Id = Guid.NewGuid();
        _sectionIds["TalkWithQuiz_Section1"] = quizTalkSection1Id;
        _sectionIds["TalkWithQuiz_Section2"] = quizTalkSection2Id;
        _sectionIds["TalkWithQuiz_Section3"] = quizTalkSection3Id;

        var talkWithQuiz = new ToolboxTalk
        {
            Id = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuiz,
            TenantId = TestTenantConstants.TenantId,
            Title = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuizTitle,
            Description = "A comprehensive safety talk with quiz assessment to verify understanding.",
            Frequency = ToolboxTalkFrequency.Monthly,
            VideoSource = VideoSource.None,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = true,
            PassingScore = 80,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-seeder",
            Sections = new List<ToolboxTalkSection>
            {
                new ToolboxTalkSection
                {
                    Id = quizTalkSection1Id,
                    ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuiz,
                    SectionNumber = 1,
                    Title = "Working at Heights",
                    Content = @"<h2>Working at Heights Safety</h2>
<p>Falls from height are one of the most common causes of workplace injuries and fatalities.</p>
<h3>Key Safety Rules:</h3>
<ol>
    <li>Always use proper fall protection above 2 meters</li>
    <li>Inspect harnesses before each use</li>
    <li>Never work alone at height</li>
    <li>Keep work areas clear of debris</li>
</ol>",
                    RequiresAcknowledgment = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test-seeder"
                },
                new ToolboxTalkSection
                {
                    Id = quizTalkSection2Id,
                    ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuiz,
                    SectionNumber = 2,
                    Title = "Ladder Safety",
                    Content = @"<h2>Ladder Safety Guidelines</h2>
<p>Proper ladder use prevents many workplace accidents.</p>
<h3>The 4:1 Rule</h3>
<p>For every 4 feet of ladder height, the base should be 1 foot away from the wall.</p>
<h3>Three Points of Contact</h3>
<p>Always maintain three points of contact when climbing - two hands and one foot, or two feet and one hand.</p>",
                    RequiresAcknowledgment = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test-seeder"
                },
                new ToolboxTalkSection
                {
                    Id = quizTalkSection3Id,
                    ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuiz,
                    SectionNumber = 3,
                    Title = "Emergency Procedures",
                    Content = @"<h2>Emergency Response</h2>
<p>Know what to do in an emergency situation.</p>
<h3>In Case of Emergency:</h3>
<ol>
    <li>Sound the alarm</li>
    <li>Call emergency services (999/112)</li>
    <li>Evacuate via nearest exit</li>
    <li>Assemble at designated muster point</li>
    <li>Do not re-enter the building</li>
</ol>",
                    RequiresAcknowledgment = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test-seeder"
                }
            },
            Questions = new List<ToolboxTalkQuestion>
            {
                new ToolboxTalkQuestion
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuiz,
                    QuestionNumber = 1,
                    QuestionText = "At what height is fall protection required?",
                    QuestionType = QuestionType.MultipleChoice,
                    Options = JsonSerializer.Serialize(new[] { "1 meter", "2 meters", "3 meters", "5 meters" }),
                    CorrectAnswer = "2 meters",
                    Points = 1,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test-seeder"
                },
                new ToolboxTalkQuestion
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuiz,
                    QuestionNumber = 2,
                    QuestionText = "You should always maintain three points of contact when climbing a ladder.",
                    QuestionType = QuestionType.TrueFalse,
                    CorrectAnswer = "True",
                    Points = 1,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test-seeder"
                },
                new ToolboxTalkQuestion
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuiz,
                    QuestionNumber = 3,
                    QuestionText = "What is the emergency phone number in Ireland?",
                    QuestionType = QuestionType.ShortAnswer,
                    CorrectAnswer = "999",
                    Points = 1,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test-seeder"
                }
            }
        };

        // 3. Talk with Video - 2 sections and video URL
        var videoTalkSection1Id = Guid.NewGuid();
        var videoTalkSection2Id = Guid.NewGuid();
        _sectionIds["TalkWithVideo_Section1"] = videoTalkSection1Id;
        _sectionIds["TalkWithVideo_Section2"] = videoTalkSection2Id;

        var talkWithVideo = new ToolboxTalk
        {
            Id = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo,
            TenantId = TestTenantConstants.TenantId,
            Title = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideoTitle,
            Description = "A video-based safety talk with supplementary reading material.",
            Frequency = ToolboxTalkFrequency.Annually,
            VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            VideoSource = VideoSource.YouTube,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-seeder",
            Sections = new List<ToolboxTalkSection>
            {
                new ToolboxTalkSection
                {
                    Id = videoTalkSection1Id,
                    ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo,
                    SectionNumber = 1,
                    Title = "Video Overview",
                    Content = @"<h2>About This Video</h2>
<p>Please watch the video above carefully. It covers important safety procedures that you must follow on site.</p>
<p><strong>Key Points to Note:</strong></p>
<ul>
    <li>Pay attention to the demonstrated procedures</li>
    <li>Note the safety equipment being used</li>
    <li>Watch for correct vs incorrect techniques</li>
</ul>",
                    RequiresAcknowledgment = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test-seeder"
                },
                new ToolboxTalkSection
                {
                    Id = videoTalkSection2Id,
                    ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo,
                    SectionNumber = 2,
                    Title = "Summary and Key Takeaways",
                    Content = @"<h2>Key Takeaways</h2>
<p>After watching the video, remember these important points:</p>
<ol>
    <li>Always follow proper procedures</li>
    <li>When in doubt, ask your supervisor</li>
    <li>Safety is everyone's responsibility</li>
</ol>
<p>If you have any questions about the content, speak with your site manager.</p>",
                    RequiresAcknowledgment = true,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "test-seeder"
                }
            }
        };

        await toolboxTalks.AddRangeAsync(new[] { basicTalk, talkWithQuiz, talkWithVideo });
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created 3 test Learnings with sections and questions");
    }

    private async Task SeedToolboxTalkSchedulesAsync()
    {
        var schedules = _context.Set<ToolboxTalkSchedule>();

        if (await schedules.IgnoreQueryFilters().AnyAsync(s => s.TenantId == TestTenantConstants.TenantId))
        {
            _logger.LogInformation("Test Learning schedules already exist, skipping");
            return;
        }

        var now = DateTime.UtcNow;

        var schedulesToCreate = new List<ToolboxTalkSchedule>
        {
            // Completed schedule (past date)
            new ToolboxTalkSchedule
            {
                Id = TestTenantConstants.ToolboxTalks.Schedules.CompletedSchedule,
                TenantId = TestTenantConstants.TenantId,
                ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk,
                ScheduledDate = now.AddDays(-14),
                Frequency = ToolboxTalkFrequency.Once,
                AssignToAllEmployees = false,
                Status = ToolboxTalkScheduleStatus.Completed,
                Notes = "Completed schedule for testing",
                CreatedAt = now.AddDays(-14),
                CreatedBy = "test-seeder"
            },
            // Active schedule (today)
            new ToolboxTalkSchedule
            {
                Id = TestTenantConstants.ToolboxTalks.Schedules.ActiveSchedule,
                TenantId = TestTenantConstants.TenantId,
                ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuiz,
                ScheduledDate = now.Date,
                Frequency = ToolboxTalkFrequency.Once,
                AssignToAllEmployees = false,
                Status = ToolboxTalkScheduleStatus.Active,
                Notes = "Active schedule for testing",
                CreatedAt = now.AddDays(-1),
                CreatedBy = "test-seeder"
            },
            // Future schedule (next week)
            new ToolboxTalkSchedule
            {
                Id = TestTenantConstants.ToolboxTalks.Schedules.FutureSchedule,
                TenantId = TestTenantConstants.TenantId,
                ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithVideo,
                ScheduledDate = now.AddDays(7),
                Frequency = ToolboxTalkFrequency.Once,
                AssignToAllEmployees = false,
                Status = ToolboxTalkScheduleStatus.Draft,
                NextRunDate = now.AddDays(7),
                Notes = "Future schedule for testing",
                CreatedAt = now,
                CreatedBy = "test-seeder"
            }
        };

        await schedules.AddRangeAsync(schedulesToCreate);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created {Count} test toolbox talk schedules", schedulesToCreate.Count);
    }

    private async Task SeedScheduledTalksAsync()
    {
        var scheduledTalks = _context.Set<ScheduledTalk>();

        if (await scheduledTalks.IgnoreQueryFilters().AnyAsync(s => s.TenantId == TestTenantConstants.TenantId))
        {
            _logger.LogInformation("Test scheduled talks already exist, skipping");
            return;
        }

        var now = DateTime.UtcNow;
        var operatorEmployeeId = TestTenantConstants.Employees.OperatorEmployee;

        // Need to add OperatorEmployee if not exists (it's in constants but may not be seeded)
        // Let's use Employee1 instead for safety
        var employeeId = TestTenantConstants.Employees.Employee1;

        var scheduledTalksToCreate = new List<ScheduledTalk>
        {
            // Pending talk
            new ScheduledTalk
            {
                Id = TestTenantConstants.ToolboxTalks.ScheduledTalks.PendingTalk,
                TenantId = TestTenantConstants.TenantId,
                ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk,
                EmployeeId = employeeId,
                ScheduleId = TestTenantConstants.ToolboxTalks.Schedules.ActiveSchedule,
                RequiredDate = now.Date,
                DueDate = now.AddDays(7),
                Status = ScheduledTalkStatus.Pending,
                RemindersSent = 0,
                LanguageCode = "en",
                VideoWatchPercent = 0,
                CreatedAt = now,
                CreatedBy = "test-seeder"
            },
            // In-progress talk (some sections read)
            new ScheduledTalk
            {
                Id = TestTenantConstants.ToolboxTalks.ScheduledTalks.InProgressTalk,
                TenantId = TestTenantConstants.TenantId,
                ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuiz,
                EmployeeId = employeeId,
                ScheduleId = TestTenantConstants.ToolboxTalks.Schedules.ActiveSchedule,
                RequiredDate = now.Date,
                DueDate = now.AddDays(7),
                Status = ScheduledTalkStatus.InProgress,
                RemindersSent = 1,
                LastReminderAt = now.AddDays(-1),
                LanguageCode = "en",
                VideoWatchPercent = 0,
                CreatedAt = now.AddDays(-2),
                CreatedBy = "test-seeder"
            },
            // Completed talk
            new ScheduledTalk
            {
                Id = TestTenantConstants.ToolboxTalks.ScheduledTalks.CompletedTalk,
                TenantId = TestTenantConstants.TenantId,
                ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk,
                EmployeeId = TestTenantConstants.Employees.Employee2,
                ScheduleId = TestTenantConstants.ToolboxTalks.Schedules.CompletedSchedule,
                RequiredDate = now.AddDays(-14),
                DueDate = now.AddDays(-7),
                Status = ScheduledTalkStatus.Completed,
                RemindersSent = 0,
                LanguageCode = "en",
                VideoWatchPercent = 0,
                CreatedAt = now.AddDays(-14),
                CreatedBy = "test-seeder"
            },
            // Overdue talk
            new ScheduledTalk
            {
                Id = TestTenantConstants.ToolboxTalks.ScheduledTalks.OverdueTalk,
                TenantId = TestTenantConstants.TenantId,
                ToolboxTalkId = TestTenantConstants.ToolboxTalks.Talks.TalkWithQuiz,
                EmployeeId = TestTenantConstants.Employees.Employee3,
                ScheduleId = TestTenantConstants.ToolboxTalks.Schedules.CompletedSchedule,
                RequiredDate = now.AddDays(-14),
                DueDate = now.AddDays(-3),
                Status = ScheduledTalkStatus.Overdue,
                RemindersSent = 5,
                LastReminderAt = now.AddDays(-1),
                LanguageCode = "en",
                VideoWatchPercent = 0,
                CreatedAt = now.AddDays(-14),
                CreatedBy = "test-seeder"
            }
        };

        await scheduledTalks.AddRangeAsync(scheduledTalksToCreate);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created {Count} test scheduled talks", scheduledTalksToCreate.Count);

        // Add section progress for InProgressTalk (first section read)
        await SeedScheduledTalkSectionProgressAsync();

        // Add completion record for CompletedTalk
        await SeedScheduledTalkCompletionAsync();
    }

    private async Task SeedScheduledTalkSectionProgressAsync()
    {
        var sectionProgress = _context.Set<ScheduledTalkSectionProgress>();

        // Mark first section of TalkWithQuiz as read for InProgressTalk
        if (_sectionIds.TryGetValue("TalkWithQuiz_Section1", out var sectionId))
        {
            var progress = new ScheduledTalkSectionProgress
            {
                Id = Guid.NewGuid(),
                ScheduledTalkId = TestTenantConstants.ToolboxTalks.ScheduledTalks.InProgressTalk,
                SectionId = sectionId,
                IsRead = true,
                ReadAt = DateTime.UtcNow.AddDays(-1),
                TimeSpentSeconds = 180, // 3 minutes
                CreatedAt = DateTime.UtcNow.AddDays(-1),
                CreatedBy = "test-seeder"
            };

            await sectionProgress.AddAsync(progress);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created section progress for in-progress talk");
        }
    }

    private async Task SeedScheduledTalkCompletionAsync()
    {
        var completions = _context.Set<ScheduledTalkCompletion>();

        var completion = new ScheduledTalkCompletion
        {
            Id = Guid.NewGuid(),
            ScheduledTalkId = TestTenantConstants.ToolboxTalks.ScheduledTalks.CompletedTalk,
            CompletedAt = DateTime.UtcNow.AddDays(-10),
            TotalTimeSpentSeconds = 600, // 10 minutes
            VideoWatchPercent = null, // No video in basic talk
            QuizScore = null, // No quiz in basic talk
            QuizMaxScore = null,
            QuizPassed = null,
            SignatureData = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            SignedAt = DateTime.UtcNow.AddDays(-10),
            SignedByName = "Jane Test",
            IPAddress = "192.168.1.100",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            CreatedBy = "test-seeder"
        };

        await completions.AddAsync(completion);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created completion record for completed talk");
    }

    #endregion
}
