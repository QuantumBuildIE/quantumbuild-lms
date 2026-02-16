using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Storage;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Slideshow;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Translations;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure;

/// <summary>
/// Dependency injection configuration for the Toolbox Talks Infrastructure layer
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Toolbox Talks Infrastructure layer services with the dependency injection container
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The configuration</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddToolboxTalksInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register email service
        services.AddScoped<IToolboxTalkEmailService, ToolboxTalkEmailService>();

        // Register reports service
        services.AddScoped<IToolboxTalkReportsService, ToolboxTalkReportsService>();

        // Register quiz generation service (question randomization and shuffling)
        services.AddScoped<IQuizGenerationService, QuizGenerationService>();

        // Register export service (stub implementation for Phase 2)
        services.AddScoped<IToolboxTalkExportService, ToolboxTalkExportService>();

        // Register subtitle processing configuration
        services.Configure<SubtitleProcessingSettings>(
            configuration.GetSection(SubtitleProcessingSettings.SectionName));

        // Register R2 storage configuration and services
        services.Configure<R2StorageSettings>(
            configuration.GetSection(R2StorageSettings.SectionName));
        services.AddScoped<ISlugGeneratorService, SlugGeneratorService>();
        services.AddScoped<IR2StorageService, R2StorageService>();

        // Register PDF extraction service (for AI content generation from uploaded PDFs)
        services.AddHttpClient<IPdfExtractionService, PdfExtractionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30); // 30 seconds for PDF download
        });

        // Register subtitle processing infrastructure services
        // ElevenLabs transcription can take a long time for large videos (download + transcription)
        services.AddHttpClient<ITranscriptionService, ElevenLabsTranscriptionService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10); // 10 minutes for video transcription
        });
        // Claude translation is usually fast but can take time for long subtitle files
        services.AddHttpClient<ITranslationService, ClaudeTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for translation
        });

        // Content translation service for translating sections and quiz questions
        services.AddHttpClient<IContentTranslationService, ContentTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for content translation
        });

        // Register SRT storage provider based on configuration
        var srtStorageType = configuration
            .GetSection($"{SubtitleProcessingSettings.SectionName}:SrtStorage:Type")
            .Value ?? "CloudflareR2";

        if (srtStorageType.Equals("CloudflareR2", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ISrtStorageProvider, CloudflareR2SrtStorageProvider>();
        }
        else
        {
            // Fall back to GitHub for backward compatibility
            services.AddHttpClient<ISrtStorageProvider, GitHubSrtStorageProvider>(client =>
            {
                client.Timeout = TimeSpan.FromMinutes(2); // 2 minutes for file uploads
            });
        }

        services.AddScoped<IVideoSourceProvider, GoogleDriveVideoSourceProvider>();
        services.AddScoped<ISubtitleProgressReporter, SignalRProgressReporter>();

        // Register subtitle processing orchestrator
        services.AddScoped<ISubtitleProcessingOrchestrator, SubtitleProcessingOrchestrator>();

        // Register transcript service for AI content generation (retrieves and parses SRT files)
        services.AddScoped<ITranscriptService, TranscriptService>();

        // Register content extraction orchestrator for AI content generation
        // Combines video transcript and PDF text extraction into a single service
        services.AddScoped<IContentExtractionService, ContentExtractionService>();

        // Register AI section generation service for generating sections from content
        services.AddHttpClient<IAiSectionGenerationService, AiSectionGenerationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3); // 3 minutes for section generation
        });

        // Register AI quiz generation service for generating quiz questions from content
        services.AddHttpClient<IAiQuizGenerationService, AiQuizGenerationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3); // 3 minutes for quiz generation
        });

        // Register the full content generation orchestrator service
        // This service coordinates extraction, section generation, and quiz generation
        services.AddScoped<IContentGenerationService, ContentGenerationService>();

        // Register content deduplication service for detecting and reusing duplicate content
        services.AddScoped<IContentDeduplicationService, ContentDeduplicationService>();

        // Register AI slideshow generation service (Claude API for HTML slideshow from PDF)
        services.AddHttpClient<IAiSlideshowGenerationService, AiSlideshowGenerationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for PDF analysis and HTML generation
        });

        // Register slideshow generation service (orchestrates PDF download + AI generation)
        services.AddHttpClient<ISlideshowGenerationService, SlideshowGenerationService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30); // 30 seconds for PDF download
        });

        // Note: SignalR hubs are registered in Program.cs with app.MapHub<>()
        //   - SubtitleProcessingHub: /api/hubs/subtitle-processing
        //   - ContentGenerationHub: /api/hubs/content-generation
        // Note: Hangfire background jobs (ContentGenerationJob, etc.) are registered in Program.cs

        return services;
    }
}
