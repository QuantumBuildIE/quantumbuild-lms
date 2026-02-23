using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Application.Abstractions.AI;
using QuantumBuild.Modules.LessonParser.Application.Abstractions;
using QuantumBuild.Modules.LessonParser.Application.Common.Interfaces;
using QuantumBuild.Modules.LessonParser.Infrastructure.Persistence;
using QuantumBuild.Modules.LessonParser.Infrastructure.Services;

namespace QuantumBuild.Modules.LessonParser.Infrastructure;

/// <summary>
/// Dependency injection configuration for the Lesson Parser Infrastructure layer
/// </summary>
public static class LessonParserInfrastructureExtensions
{
    /// <summary>
    /// Registers Lesson Parser Infrastructure layer services with the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddLessonParserInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register LessonParserDbContext with the same connection string as the main ApplicationDbContext
        services.AddDbContext<LessonParserDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            }));

        // Register ILessonParserDbContext
        services.AddScoped<ILessonParserDbContext>(provider =>
            provider.GetRequiredService<LessonParserDbContext>());

        // Register document extraction service
        services.AddScoped<IDocumentExtractor, DocumentExtractorService>();

        // Bind Claude API settings from the shared config section
        services.Configure<ClaudeSettings>(
            configuration.GetSection(ClaudeSettings.SectionName));

        // Register named HttpClient for URL content fetching
        services.AddHttpClient("LessonParser", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 QuantumBuild-LessonParser/1.0");
        });

        // Register lesson generator service (Claude AI → ToolboxTalks + Course)
        services.AddHttpClient<ILessonGeneratorService, LessonGeneratorService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for AI content generation
        });

        return services;
    }
}
