using Microsoft.Extensions.DependencyInjection;

namespace QuantumBuild.Modules.LessonParser.Application;

/// <summary>
/// Dependency injection configuration for the Lesson Parser Application layer
/// </summary>
public static class LessonParserApplicationExtensions
{
    /// <summary>
    /// Registers Lesson Parser Application layer services with the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddLessonParserApplication(this IServiceCollection services)
    {
        // Services will be added in later tasks

        return services;
    }
}
