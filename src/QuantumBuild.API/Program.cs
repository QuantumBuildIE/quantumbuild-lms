using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using QuantumBuild.Core.Application;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Core.Infrastructure.Identity;
using QuantumBuild.Core.Infrastructure.Persistence;
using QuantumBuild.Core.Infrastructure.Repositories;
using QuantumBuild.Core.Application.Abstractions.Email;
using QuantumBuild.Core.Infrastructure.Services;
using QuantumBuild.Core.Infrastructure.Services.Email;
using QuantumBuild.Modules.ToolboxTalks.Application;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Persistence.Seed;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;
using QuantumBuild.Modules.LessonParser.Application;
using QuantumBuild.Modules.LessonParser.Infrastructure;
using QuantumBuild.Modules.LessonParser.Infrastructure.Hubs;
using QuantumBuild.Modules.LessonParser.Infrastructure.Jobs;
using QuantumBuild.Core.Application.Http;
using QuantumBuild.Modules.LessonParser.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Add CORS for frontend development
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins(
                "http://localhost:3000",
                "https://quantumbuild-lms-web-production.up.railway.app",
                "https://quantumbuild-lms-web-development.up.railway.app",
                "https://quantumbuild-lms-web-demo.up.railway.app"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure PostgreSQL database with transient fault retry
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    }));

// Register ICoreDbContext
builder.Services.AddScoped<ICoreDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// Register IToolboxTalksDbContext
builder.Services.AddScoped<IToolboxTalksDbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// Register ToolboxTalks module services
builder.Services.AddToolboxTalksInfrastructure(builder.Configuration);

// Register LessonParser module services
builder.Services.AddLessonParserApplication();
builder.Services.AddLessonParserInfrastructure(builder.Configuration);

// Register DbContext (for DataSeeder)
builder.Services.AddScoped<DbContext>(provider =>
    provider.GetRequiredService<ApplicationDbContext>());

// Add Identity services with JWT authentication
builder.Services.AddIdentityServices<ApplicationDbContext>(builder.Configuration);

// Add permission-based authorization policies
builder.Services.AddPermissionPolicies(Permissions.GetAll());

// Register Application layer services
builder.Services.AddCoreApplication();
builder.Services.AddToolboxTalksApplication();

// Register HttpContextAccessor for accessing current user from JWT
builder.Services.AddHttpContextAccessor();

// Register HttpClient for Claude API
builder.Services.AddHttpClient("ClaudeApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
    sp.GetRequiredService<ILogger<Program>>()));

// Register Infrastructure services
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<ISystemAuditLogger, SystemAuditLogger>();

// Register Email Provider
builder.Services.Configure<EmailProviderSettings>(
    builder.Configuration.GetSection(EmailProviderSettings.SectionName));

var emailProvider = builder.Configuration.GetValue<string>("EmailProvider:Provider");
if (string.Equals(emailProvider, "MailerSend", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmailProvider, MailerSendEmailProvider>((sp, client) =>
    {
        var apiKey = builder.Configuration.GetValue<string>("EmailProvider:ApiKey");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    });
}
else
{
    builder.Services.AddSingleton<IEmailProvider, StubEmailProvider>();
}

// Register background jobs
builder.Services.AddScoped<ProcessToolboxTalkSchedulesJob>();
builder.Services.AddScoped<SendToolboxTalkRemindersJob>();
builder.Services.AddScoped<UpdateOverdueToolboxTalksJob>();
builder.Services.AddScoped<ContentGenerationJob>();
builder.Services.AddScoped<TranslationValidationJob>();
builder.Services.AddScoped<DailyTranslationScanJob>();
builder.Services.AddScoped<ExpiredSessionCleanupJob>();
builder.Services.AddScoped<LessonParseJob>();
builder.Services.AddScoped<VideoTranscriptionJob>();
builder.Services.AddScoped<ContentCreationParseJob>();
builder.Services.AddScoped<RequirementIngestionJob>();
builder.Services.AddScoped<AggregateAiUsageJob>();

// Add Hangfire with PostgreSQL storage
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options
        .UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));

builder.Services.AddHangfireServer(options =>
{
    options.Queues = new[] { "default", "content-generation" };
});

// Add controllers with JSON options for enum string conversion and camelCase naming
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Add Swagger/OpenAPI documentation with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "QuantumBuild LMS API",
        Version = "v1",
        Description = "API for the QuantumBuild LMS"
    });

    // Add JWT authentication support in Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token. The 'Bearer ' prefix will be added automatically."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add health checks
var healthChecksBuilder = builder.Services.AddHealthChecks();

// Only add database health check if connection string is available (skipped in testing environment)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
{
    healthChecksBuilder.AddNpgSql(connectionString, name: "database");
}

// Add SignalR for real-time subtitle processing progress updates
builder.Services.AddSignalR();

var app = builder.Build();

// Apply database migrations on startup with retry for transient failures
{
    var maxRetries = 5;
    var delay = TimeSpan.FromSeconds(5);

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();

            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending migration(s): {Migrations}",
                    pendingMigrations.Count,
                    string.Join(", ", pendingMigrations));

                await context.Database.MigrateAsync();

                logger.LogInformation("Database migrations applied successfully");
            }
            else
            {
                logger.LogInformation("Database schema is up to date");
            }
            break;
        }
        catch (Exception ex) when (i < maxRetries - 1)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex,
                "Database connection failed on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}s...",
                i + 1, maxRetries, delay.TotalSeconds);
            await Task.Delay(delay);
            delay *= 2; // Exponential backoff
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogCritical(ex, "Failed to apply database migrations after {MaxRetries} attempts", maxRetries);
            throw;
        }
    }
}

// Apply Lesson Parser module migrations
{
    var maxRetries = 5;
    var delay = TimeSpan.FromSeconds(5);

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LessonParserDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

            var pendingMigrations = (await context.Database.GetPendingMigrationsAsync()).ToList();

            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} Lesson Parser migration(s): {Migrations}",
                    pendingMigrations.Count,
                    string.Join(", ", pendingMigrations));

                await context.Database.MigrateAsync();

                logger.LogInformation("Lesson Parser migrations applied successfully");
            }
            else
            {
                logger.LogInformation("Lesson Parser schema is up to date");
            }
            break;
        }
        catch (Exception ex) when (i < maxRetries - 1)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex,
                "Lesson Parser migration failed on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}s...",
                i + 1, maxRetries, delay.TotalSeconds);
            await Task.Delay(delay);
            delay *= 2;
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogCritical(ex, "Failed to apply Lesson Parser migrations after {MaxRetries} attempts", maxRetries);
            throw;
        }
    }
}

// Seed database with initial data
await DataSeeder.SeedAsync(app.Services);

// Seed Toolbox Talks module data
await SeedToolboxTalksDataAsync(app.Services);

// Configure the HTTP request pipeline.

// Global exception handler — returns RFC 9110 Problem Details JSON, never exposes internals
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

        if (exceptionFeature?.Error is { } ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            title = "An unexpected error occurred",
            status = 500,
            detail = "An internal error occurred. Please try again shortly."
        });
    });
});

// Return JSON Problem Details for bare status codes (404, 405, etc.) instead of empty responses
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    response.ContentType = "application/problem+json";

    await response.WriteAsJsonAsync(new
    {
        type = $"https://tools.ietf.org/html/rfc9110#section-15.{(response.StatusCode >= 500 ? 6 : 5)}.{(response.StatusCode % 100) + 1}",
        title = response.StatusCode switch
        {
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            _ => "Error"
        },
        status = response.StatusCode,
        detail = response.StatusCode switch
        {
            404 => "The requested resource was not found.",
            405 => "The HTTP method is not allowed for this resource.",
            _ => "An error occurred."
        }
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "QuantumBuild LMS API v1");
        options.RoutePrefix = string.Empty; // Set Swagger UI at the app's root
    });
}

app.UseHttpsRedirection();

// Enable CORS for development
app.UseCors("Development");

// Enable static files (for product images)
app.UseStaticFiles();

// Enable WebSocket support (required for SignalR WebSocket transport)
app.UseWebSockets();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Map SignalR hubs
app.MapHub<SubtitleProcessingHub>("/api/hubs/subtitle-processing");
app.MapHub<ContentGenerationHub>("/api/hubs/content-generation");
app.MapHub<TranslationValidationHub>("/api/hubs/translation-validation");
app.MapHub<CorpusRunHub>("/api/hubs/corpus-run");
app.MapHub<LessonParserHub>("/api/hubs/lesson-parser");

// Map health check endpoint
app.MapHealthChecks("/health");

// Configure Hangfire dashboard (only in development for security)
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

// Register recurring jobs using DI-based approach (required for production)
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    var irelandTimeZone = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    // Toolbox Talks background jobs
    recurringJobManager.AddOrUpdate<ProcessToolboxTalkSchedulesJob>(
        "process-toolbox-talk-schedules",
        job => job.ExecuteAsync(CancellationToken.None),
        "30 6 * * *", // Run at 6:30 AM daily
        new RecurringJobOptions { TimeZone = irelandTimeZone });

    recurringJobManager.AddOrUpdate<SendToolboxTalkRemindersJob>(
        "send-toolbox-talk-reminders",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 8 * * *", // Run at 8:00 AM daily
        new RecurringJobOptions { TimeZone = irelandTimeZone });

    recurringJobManager.AddOrUpdate<UpdateOverdueToolboxTalksJob>(
        "update-overdue-toolbox-talks",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 * * * *"); // Run every hour

    recurringJobManager.AddOrUpdate<SendRefresherRemindersJob>(
        "send-refresher-reminders",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 9 * * *", // Run daily at 9:00 AM
        new RecurringJobOptions { TimeZone = irelandTimeZone });

    recurringJobManager.AddOrUpdate<DailyTranslationScanJob>(
        "daily-translation-scan",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily(2, 0)); // 2am UTC daily

    recurringJobManager.AddOrUpdate<ExpiredSessionCleanupJob>(
        "expired-session-cleanup",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily(3, 0)); // 3am UTC daily

    recurringJobManager.AddOrUpdate<AggregateAiUsageJob>(
        "aggregate-ai-usage",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 3 1 * *"); // 1st of each month at 3am UTC
}

app.Run();

/// <summary>
/// Seeds Toolbox Talks module data using the main ApplicationDbContext
/// </summary>
static async Task SeedToolboxTalksDataAsync(IServiceProvider serviceProvider)
{
    using var scope = serviceProvider.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var context = services.GetRequiredService<DbContext>();
        await ToolboxTalksSeedData.SeedAsync(context, logger);
        await SafetyGlossarySeedData.SeedAsync(context, logger);
        await SectorSeedData.SeedAsync(context, logger);
        await RegulatoryProfileSeedData.SeedAsync(context, logger);
        await RegulatoryRequirementSeedData.SeedAsync(context, logger);

        // Ensure the active pipeline version record exists on first startup
        var pipelineVersionService = services.GetRequiredService<IPipelineVersionService>();
        await pipelineVersionService.GetOrCreateCurrentAsync();

        logger.LogInformation("Learnings module seeding completed");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error seeding Learnings module data");
        throw;
    }
}

// Make the Program class public so integration tests can access it
public partial class Program { }
