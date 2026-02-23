using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Modules.LessonParser.Application.Common.Interfaces;
using QuantumBuild.Modules.LessonParser.Infrastructure.Persistence;

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

        return services;
    }
}
