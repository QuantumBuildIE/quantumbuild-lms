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
using QuantumBuild.Core.Application.Abstractions;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Core.Application.Features.BulkImport;
using QuantumBuild.Core.Infrastructure.Jobs;
using Microsoft.Extensions.Options;
using Sentry;
using Sentry.Extensibility;
using Sentry.Protocol;
using System.Text.RegularExpressions;
using QuantumBuild.API.Monitoring;

var builder = WebApplication.CreateBuilder(args);

// Sentry: error-only monitoring. Inert when no DSN is configured (SENTRY_DSN env
// var or Sentry:Dsn config) so the app runs normally in environments before the
// DSN is set. No performance tracing, no log-forwarding — errors only.
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["SENTRY_DSN"] ?? builder.Configuration["Sentry:Dsn"];
    options.SendDefaultPii = false;
    options.Environment = builder.Configuration["Sentry:Environment"] ?? builder.Environment.EnvironmentName;
    options.MinimumEventLevel = LogLevel.Error;
    // Defence-in-depth #1: never extract request bodies. This is already the
    // SDK default (RequestSize.None - opt-in required), set explicitly so it
    // can't drift silently if a future Sentry version changes its default.
    options.MaxRequestBodySize = RequestSize.None;
    // Defence-in-depth #2: scrub clear-text PII from every event before it
    // leaves the process - email addresses, the Authorization header (and
    // other credential-bearing headers), and the raw query string (reset/
    // invite links carry single-use tokens as query params). The user GUID
    // (scope.User.Id) is left untouched: with SendDefaultPii false, Sentry's
    // claims-based DefaultUserFactory never runs, so this GUID is not the
    // caller's identity - it's Sentry's own per-installation InstallationId,
    // attached as a fallback by the core SDK's Enricher whenever User.Id is
    // still null. It is pseudonymous and constant per server instance/DSN,
    // not per request or per employee.
    options.SetBeforeSend(ScrubSentryEvent);
#if DEBUG
    options.Debug = true;
#endif
});

// Add services to the container.

// Add CORS for frontend development
builder.Services.AddCors(options =>
{
    options.AddPolicy("CertifiedIQ", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();
            
        policy.WithOrigins(allowedOrigins)
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
    sp.GetRequiredService<ILogger<Program>>()))
.AddPolicyHandler((sp, _) => sp.GetRequiredService<ProviderBulkheadPolicies>().Anthropic);

// Register Infrastructure services
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IJobTenantContextAccessor, JobTenantContextAccessor>();
builder.Services.AddScoped<ITenantRepository, TenantRepository>();
builder.Services.AddScoped<ISystemAuditLogger, SystemAuditLogger>();

// Register Email Provider
builder.Services.Configure<EmailProviderSettings>(
    builder.Configuration.GetSection(EmailProviderSettings.SectionName));

builder.Services.Configure<BulkImportSettings>(
    builder.Configuration.GetSection(BulkImportSettings.SectionName));

builder.Services.AddOptions<AIProviderOptions>()
    .BindConfiguration(AIProviderOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AIProviderOptions>, AIProviderOptionsValidator>();

builder.Services.AddOptions<ProviderConcurrencyOptions>()
    .BindConfiguration(ProviderConcurrencyOptions.SectionName)
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<ProviderConcurrencyOptions>, ProviderConcurrencyOptionsValidator>();
builder.Services.AddSingleton<ProviderBulkheadPolicies>();

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
builder.Services.AddScoped<BulkLearningTranslationSweepJob>();
builder.Services.AddScoped<ExpiredSessionCleanupJob>();
builder.Services.AddScoped<LessonParseJob>();
builder.Services.AddScoped<VideoTranscriptionJob>();
builder.Services.AddScoped<ContentCreationParseJob>();
builder.Services.AddScoped<RequirementIngestionJob>();
builder.Services.AddScoped<StaleIngestionSweepJob>();
builder.Services.AddScoped<AggregateAiUsageJob>();
builder.Services.AddScoped<IGenerateEmployeePinsJob, GenerateEmployeePinsJob>();
builder.Services.AddScoped<IBulkEmployeeImportJob, BulkEmployeeImportJob>();
builder.Services.AddScoped<IBulkSopImportJob, BulkSopImportJob>();

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

// Report Hangfire jobs that reach the Failed state to Sentry - background jobs run outside
// the web pipeline, so the ASP.NET Sentry integration never sees them otherwise. See
// HangfireSentryJobFilter for why IApplyStateFilter is used instead of IElectStateFilter.
Hangfire.GlobalJobFilters.Filters.Add(new HangfireSentryJobFilter());

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
// KeepAliveInterval: server pings client every 10 s — defeats Railway proxy idle timeout (assumed ~60 s)
// ClientTimeoutInterval: server waits 2 min before treating a silent client as disconnected
// Change is global; all five registered hubs (SubtitleProcessingHub, ContentGenerationHub,
// TranslationValidationHub, CorpusRunHub, LessonParserHub) inherit these settings.
// Shorter keep-alive is strictly more conservative; longer client timeout is strictly more lenient — safe for all hubs.
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(10);
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(2);
});

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

        // A FluentValidation failure that bubbles up past a controller with no explicit catch
        // block (e.g. thrown by the MediatR ValidationBehavior) is a client input error, not a
        // server fault — surface it as 400 with the standard Result envelope so the frontend's
        // getApiErrorMessage renders the actual field messages instead of a generic 500.
        if (exceptionFeature?.Error is FluentValidation.ValidationException validationEx)
        {
            logger.LogWarning(validationEx, "Validation failed on {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var validationJsonOptions = new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase };
            validationJsonOptions.Converters.Add(new JsonStringEnumConverter());

            await context.Response.WriteAsJsonAsync(
                QuantumBuild.Core.Application.Models.Result.Fail(validationEx),
                validationJsonOptions);
            return;
        }

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
app.UseCors("CertifiedIQ");

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

    recurringJobManager.AddOrUpdate<BulkLearningTranslationSweepJob>(
        "bulk-learning-translation-sweep",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily(1, 0)); // 1am UTC daily — off-peak, ahead of the 2am/3am/4am sweep block

    recurringJobManager.AddOrUpdate<ExpiredSessionCleanupJob>(
        "expired-session-cleanup",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily(3, 0)); // 3am UTC daily

    recurringJobManager.AddOrUpdate<StaleIngestionSweepJob>(
        "stale-ingestion-sweep",
        job => job.ExecuteAsync(CancellationToken.None),
        Cron.Daily(4, 0)); // 4am UTC daily

    recurringJobManager.AddOrUpdate<AggregateAiUsageJob>(
        "aggregate-ai-usage",
        job => job.ExecuteAsync(CancellationToken.None),
        "0 3 1 * *"); // 1st of each month at 3am UTC
}

app.Run();

/// <summary>
/// Redacts clear-text PII (currently: email addresses) from a string,
/// leaving everything else - including the user GUID and stack traces -
/// untouched.
/// </summary>
static string? RedactPii(string? text)
{
    if (string.IsNullOrEmpty(text))
    {
        return text;
    }

    foreach (var (pattern, replacement) in Program.PiiRedactionPatterns)
    {
        text = pattern.Replace(text, replacement);
    }

    return text;
}

/// <summary>
/// Sentry BeforeSend hook: strips clear-text PII from the event message and
/// exception messages, strips credential-bearing request headers, and drops
/// the request query string, before the event leaves the process. Does not
/// touch scope.User (Sentry's own pseudonymous installation GUID - see the
/// comment on options.SetBeforeSend above), stack traces, or other context.
/// </summary>
static SentryEvent? ScrubSentryEvent(SentryEvent @event)
{
    if (@event.Message is { } message)
    {
        message.Message = RedactPii(message.Message);
        message.Formatted = RedactPii(message.Formatted);
    }

    foreach (var exception in @event.SentryExceptions ?? Enumerable.Empty<SentryException>())
    {
        exception.Value = RedactPii(exception.Value);
    }

    // Belt-and-braces alongside MaxRequestBodySize = None above: make sure no
    // request body ever rides along on an event.
    @event.Request.Data = null;

    // Defence-in-depth #3: the ASP.NET Core integration copies every request
    // header verbatim (Cookie excepted) regardless of SendDefaultPii, so the
    // Authorization bearer JWT would otherwise reach Sentry in clear text.
    // Denylist rather than allowlist, kept simple on purpose: an exact-name
    // list of known credential headers plus a substring fallback so a future
    // custom header carrying a token/secret/key/auth value is still caught.
    if (@event.Request.Headers is { } headers && headers.Count > 0)
    {
        foreach (var headerName in headers.Keys.ToList())
        {
            if (IsSensitiveRequestHeader(headerName))
            {
                headers.Remove(headerName);
            }
        }
    }

    // Defence-in-depth #4: drop the raw query string outright rather than
    // attempt to redact it. Query params carry single-use reset/invite
    // tokens, and the integration sets QueryString unconditionally
    // (unlike Cookie/User, it is not gated by SendDefaultPii). Request.Url
    // is unaffected - the SDK builds it as scheme://host+path and never
    // appends the query string.
    @event.Request.QueryString = null;

    return @event;
}

/// <summary>
/// True if a request header name is a known or likely credential carrier
/// (Authorization, API keys, tokens, secrets) and must never reach Sentry.
/// </summary>
static bool IsSensitiveRequestHeader(string headerName)
{
    foreach (var exact in Program.SensitiveRequestHeaderNames)
    {
        if (string.Equals(headerName, exact, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    foreach (var fragment in Program.SensitiveRequestHeaderNameFragments)
    {
        if (headerName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

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
        await RegulatoryStructureMapSeedData.SeedAsync(context, logger);

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
public partial class Program
{
    // Clear-text PII patterns to redact from Sentry event text, applied in
    // order. Add further patterns here (e.g. phone numbers) as new clear-text
    // PII shapes are identified - keep each pattern its own conservative tuple.
    internal static readonly (Regex Pattern, string Replacement)[] PiiRedactionPatterns =
    {
        (new Regex(@"[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+",
            RegexOptions.Compiled), "[redacted-email]"),
    };

    // Request header names stripped verbatim from Sentry events (case-insensitive).
    internal static readonly string[] SensitiveRequestHeaderNames =
    {
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
        "X-Api-Key",
    };

    // Substrings that flag a header name as credential-bearing even if it
    // isn't in the exact-name list above (e.g. a future custom header).
    internal static readonly string[] SensitiveRequestHeaderNameFragments =
    {
        "token",
        "secret",
        "api-key",
        "apikey",
        "auth",
    };
}
