using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Core.Infrastructure.Identity;
using QuantumBuild.Core.Infrastructure.Persistence;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Tests.Common.TestTenant;
using QuantumBuild.Tests.Integration.Setup.Fakes;
using Respawn;
using Testcontainers.PostgreSql;

namespace QuantumBuild.Tests.Integration.Fixtures;

/// <summary>
/// Custom WebApplicationFactory that uses Testcontainers for PostgreSQL.
/// Provides an isolated database for each test run with authentication support.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("quantumbuild_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private Respawner? _respawner;
    private bool _isSeeded = false;
    private bool _isMigrated = false;

    /// <summary>
    /// Gets the connection string for the test database.
    /// </summary>
    public string ConnectionString => _dbContainer.GetConnectionString();

    /// <summary>
    /// JWT settings used for generating test tokens.
    /// </summary>
    private static readonly JwtSettings TestJwtSettings = new()
    {
        Secret = "TestSecretKeyThatIsAtLeast32CharactersLong!ForIntegrationTests!",
        Issuer = "QuantumBuildManagementSystem",
        Audience = "QuantumBuildManagementSystemUsers",
        ExpiryMinutes = 60,
        RefreshTokenExpiryDays = 7
    };

    /// <summary>
    /// Fake email sender for capturing sent emails in tests.
    /// </summary>
    public FakeEmailSender FakeEmailSender { get; } = new();

    /// <summary>
    /// Fake subtitle services for testing subtitle processing without external APIs.
    /// </summary>
    public FakeTranscriptionService FakeTranscriptionService { get; } = new();
    public FakeTranslationService FakeTranslationService { get; } = new();
    public FakeSrtStorageProvider FakeSrtStorageProvider { get; } = new();
    public FakeVideoSourceProvider FakeVideoSourceProvider { get; } = new();
    public FakeSubtitleProgressReporter FakeSubtitleProgressReporter { get; } = new();
    public FakeR2StorageService FakeR2StorageService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configure Hangfire to use in-memory storage BEFORE services are built
        // This must be done early to prevent PostgreSQL storage initialization
        GlobalConfiguration.Configuration.UseMemoryStorage();

        // Set the connection string in configuration BEFORE Program.cs runs
        // This is required because health checks read from configuration during service registration
        // Capture the container reference to get the connection string at execution time
        var container = _dbContainer;
        builder.ConfigureAppConfiguration((context, config) =>
        {
            var connStr = container.GetConnectionString();
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connStr,
                // Add JWT settings to configuration so they're available during service registration
                ["JwtSettings:Secret"] = "TestSecretKeyThatIsAtLeast32CharactersLong!ForIntegrationTests!",
                ["JwtSettings:Issuer"] = "QuantumBuildManagementSystem",
                ["JwtSettings:Audience"] = "QuantumBuildManagementSystemUsers",
                ["JwtSettings:ExpiryMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpiryDays"] = "7"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove all Hangfire service registrations to prevent PostgreSQL storage errors
            var hangfireDescriptors = services.Where(d =>
                d.ServiceType == typeof(JobStorage) ||
                d.ImplementationType?.Namespace?.Contains("Hangfire") == true ||
                d.ServiceType.Namespace?.Contains("Hangfire") == true).ToList();

            foreach (var descriptor in hangfireDescriptors)
            {
                services.Remove(descriptor);
            }

            // Re-add Hangfire with in-memory storage for testing
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseMemoryStorage());

            // Don't add Hangfire server in tests - we don't need background job processing
            // services.AddHangfireServer();
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove the existing DbContext configuration
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)).ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Add DbContext using the test container connection string
            // Suppress pending model changes warning for testing (schema drift doesn't matter for tests)
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(ConnectionString);
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Override JWT settings for testing
            services.Configure<JwtSettings>(options =>
            {
                options.Secret = TestJwtSettings.Secret;
                options.Issuer = TestJwtSettings.Issuer;
                options.Audience = TestJwtSettings.Audience;
                options.ExpiryMinutes = TestJwtSettings.ExpiryMinutes;
                options.RefreshTokenExpiryDays = TestJwtSettings.RefreshTokenExpiryDays;
            });

            // Reconfigure JWT Bearer authentication to use test settings
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = TestJwtSettings.Issuer,
                    ValidAudience = TestJwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSettings.Secret)),
                    ClockSkew = TimeSpan.Zero
                };
            });

            // Run migrations BEFORE the app tries to seed data
            // This is critical - the DataSeeder will fail if tables don't exist
            if (!_isMigrated)
            {
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();

                var appContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                appContext.Database.Migrate();

                _isMigrated = true;
            }

            // Register fake services for testing
            // services.AddSingleton<IEmailSender>(FakeEmailSender);

            // Register fake subtitle processing services to avoid external API calls
            services.RemoveAll<ITranscriptionService>();
            services.RemoveAll<ITranslationService>();
            services.RemoveAll<ISrtStorageProvider>();
            services.RemoveAll<IVideoSourceProvider>();
            services.RemoveAll<ISubtitleProgressReporter>();

            services.AddSingleton<ITranscriptionService>(FakeTranscriptionService);
            services.AddSingleton<ITranslationService>(FakeTranslationService);
            services.AddSingleton<ISrtStorageProvider>(FakeSrtStorageProvider);
            services.AddSingleton<IVideoSourceProvider>(FakeVideoSourceProvider);
            services.AddSingleton<ISubtitleProgressReporter>(FakeSubtitleProgressReporter);

            // Replace R2 storage service to avoid requiring AWS/R2 configuration
            services.RemoveAll<IR2StorageService>();
            services.AddSingleton<IR2StorageService>(FakeR2StorageService);
        });

        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        // Start the database container first
        await _dbContainer.StartAsync();

        // Accessing Services triggers host creation, which runs ConfigureWebHost
        // where migrations are applied, and then Program.cs runs with DataSeeder
        _ = Services;

        // Initialize Respawner for database reset
        using var npgsqlConnection = new Npgsql.NpgsqlConnection(ConnectionString);
        await npgsqlConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(npgsqlConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" },
            TablesToIgnore = new Respawn.Graph.Table[]
            {
                "__EFMigrationsHistory",
                // Identity tables - preserve user and role data (custom table names)
                "Roles",
                "RoleClaims",
                "Users",
                "UserClaims",
                "UserLogins",
                "UserRoles",
                "UserTokens",
                // System permissions
                "Permissions",
                "RolePermissions",
                // Tenant data is seeded by DataSeeder and TestTenantSeeder
                "Tenants"
            }
        });
        await npgsqlConnection.CloseAsync();

        // Seed test tenant data after database is ready
        await SeedTestTenantAsync();
    }

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    /// Resets the database to a clean state while preserving system data.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner != null)
        {
            using var npgsqlConnection = new Npgsql.NpgsqlConnection(ConnectionString);
            await npgsqlConnection.OpenAsync();
            await _respawner.ResetAsync(npgsqlConnection);
            await npgsqlConnection.CloseAsync();
        }

        // Re-seed default QUANTUMBUILD tenant data (Sites, Categories, Products, etc.)
        // This is needed because Respawner cleans these tables
        await SeedDefaultTenantDataAsync();

        // Re-seed test tenant data
        _isSeeded = false;
        await SeedTestTenantAsync();
    }

    /// <summary>
    /// Seeds the default QUANTUMBUILD tenant data that was cleaned by Respawner.
    /// NOTE: Tests should ONLY use test tenant data, not QUANTUMBUILD tenant data.
    /// This method is intentionally left empty to enforce test isolation.
    /// </summary>
    private Task SeedDefaultTenantDataAsync()
    {
        // Tests should NEVER query QUANTUMBUILD tenant data - only test tenant data.
        // The TestTenantSeeder provides all data needed for integration tests.
        return Task.CompletedTask;
    }

    /// <summary>
    /// Seeds the test tenant data into the database.
    /// Uses UserManager for proper password hashing so test users can login via the auth endpoint.
    /// </summary>
    private async Task SeedTestTenantAsync()
    {
        if (_isSeeded) return;

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<TestTenantSeeder>>();

        // Pass the service provider so TestTenantSeeder can use UserManager for password hashing
        var seeder = new TestTenantSeeder(context, logger, scope.ServiceProvider);
        await seeder.SeedAllAsync();
        _isSeeded = true;
    }

    /// <summary>
    /// Creates an HTTP client authenticated as the specified test user type.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(TestUserType userType)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = GenerateTestToken(userType);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// Creates an HTTP client authenticated with the specified user ID and permissions.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(Guid userId, string email, IEnumerable<string> roles, IEnumerable<string> permissions)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var token = GenerateTestToken(userId, email, TestTenantConstants.TenantId, roles, permissions);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// Generates a valid JWT token for the specified test user type.
    /// </summary>
    private string GenerateTestToken(TestUserType userType)
    {
        var (userId, email, firstName, lastName, roles, permissions, employeeId) = userType switch
        {
            TestUserType.Admin => (
                TestTenantConstants.Users.Admin.Id,
                TestTenantConstants.Users.Admin.Email,
                TestTenantConstants.Users.Admin.FirstName,
                TestTenantConstants.Users.Admin.LastName,
                new[] { "Admin" },
                Permissions.GetAll(),
                (Guid?)null // Admin may not have employee record
            ),
            TestUserType.SiteManager => (
                TestTenantConstants.Users.SiteManager.Id,
                TestTenantConstants.Users.SiteManager.Email,
                TestTenantConstants.Users.SiteManager.FirstName,
                TestTenantConstants.Users.SiteManager.LastName,
                new[] { "SiteManager" },
                new[] {
                    "ToolboxTalks.View", "ToolboxTalks.Edit", "ToolboxTalks.Schedule"
                },
                (Guid?)TestTenantConstants.Employees.ManagerEmployee // Link to manager employee
            ),
            TestUserType.Warehouse => (
                TestTenantConstants.Users.Warehouse.Id,
                TestTenantConstants.Users.Warehouse.Email,
                TestTenantConstants.Users.Warehouse.FirstName,
                TestTenantConstants.Users.Warehouse.LastName,
                new[] { "Warehouse" },
                new[] {
                    "ToolboxTalks.View"
                },
                (Guid?)null
            ),
            TestUserType.Operator => (
                TestTenantConstants.Users.Operator.Id,
                TestTenantConstants.Users.Operator.Email,
                TestTenantConstants.Users.Operator.FirstName,
                TestTenantConstants.Users.Operator.LastName,
                new[] { "Operator" },
                new[] {
                    "ToolboxTalks.View"
                },
                (Guid?)TestTenantConstants.Employees.OperatorEmployee // Link to operator employee
            ),
            TestUserType.Finance => (
                TestTenantConstants.Users.Finance.Id,
                TestTenantConstants.Users.Finance.Email,
                TestTenantConstants.Users.Finance.FirstName,
                TestTenantConstants.Users.Finance.LastName,
                new[] { "Finance" },
                new[] {
                    "ToolboxTalks.View"
                },
                (Guid?)null
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(userType))
        };

        return GenerateTestToken(userId, email, TestTenantConstants.TenantId, roles, permissions, firstName, lastName, employeeId);
    }

    /// <summary>
    /// Generates a valid JWT token with the specified claims.
    /// </summary>
    private string GenerateTestToken(
        Guid userId,
        string email,
        Guid tenantId,
        IEnumerable<string> roles,
        IEnumerable<string> permissions,
        string firstName = "Test",
        string lastName = "User",
        Guid? employeeId = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.GivenName, firstName),
            new(ClaimTypes.Surname, lastName),
            new("tenant_id", tenantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // Add employee_id claim if user has an associated employee
        if (employeeId.HasValue)
        {
            claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        }

        // Add role claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        // Add permission claims
        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(TestJwtSettings.ExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: TestJwtSettings.Issuer,
            audience: TestJwtSettings.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>
/// Predefined test user types with different permission sets.
/// </summary>
public enum TestUserType
{
    /// <summary>
    /// Admin user with all permissions.
    /// </summary>
    Admin,

    /// <summary>
    /// Site Manager with site and attendance management permissions.
    /// </summary>
    SiteManager,

    /// <summary>
    /// Warehouse staff with stock management permissions.
    /// </summary>
    Warehouse,

    /// <summary>
    /// Operator with limited view and basic action permissions.
    /// </summary>
    Operator,

    /// <summary>
    /// Finance user with view and costing permissions.
    /// </summary>
    Finance
}
