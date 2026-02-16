using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;

namespace QuantumBuild.Modules.ToolboxTalks.Application;

/// <summary>
/// Dependency injection configuration for the Toolbox Talks Application layer
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers Toolbox Talks Application layer services with the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddToolboxTalksApplication(this IServiceCollection services)
    {
        // Register MediatR handlers from this assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Register subtitle processing application services
        services.AddScoped<ISrtGeneratorService, SrtGeneratorService>();
        services.AddScoped<ILanguageCodeService, LanguageCodeService>();

        // Register course progress service
        services.AddScoped<ICourseProgressService, Services.CourseProgressService>();

        // Register refresher scheduling service
        services.AddScoped<IRefresherSchedulingService, Services.RefresherSchedulingService>();

        // Register certificate generation service
        services.AddScoped<ICertificateGenerationService, Services.CertificateGenerationService>();

        // Register auto-assignment service for new employees
        services.AddScoped<INewEmployeeTrainingAssigner, AutoAssignmentService>();

        return services;
    }
}
