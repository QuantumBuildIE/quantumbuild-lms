using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Application.Interfaces;
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
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Mapping;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Sectors;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Mapping;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Sectors;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Ingestion;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Http;

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
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetElevenLabsPolicy(
            sp.GetRequiredService<ILogger<ElevenLabsTranscriptionService>>()));
        // Claude translation is usually fast but can take time for long subtitle files
        services.AddHttpClient<ITranslationService, ClaudeTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for translation
        });

        // Content translation service for translating sections and quiz questions
        services.AddHttpClient<IContentTranslationService, ContentTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for content translation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<ContentTranslationService>>()));

        // Translation job scheduler (fire-and-forget Hangfire enqueue, used by cross-module callers)
        services.AddSingleton<ITranslationJobScheduler, TranslationJobScheduler>();

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
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<AiSectionGenerationService>>()));

        // Register AI quiz generation service for generating quiz questions from content
        services.AddHttpClient<IAiQuizGenerationService, AiQuizGenerationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3); // 3 minutes for quiz generation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<AiQuizGenerationService>>()));

        // Register the full content generation orchestrator service
        // This service coordinates extraction, section generation, and quiz generation
        services.AddScoped<IContentGenerationService, ContentGenerationService>();

        // Register content deduplication service for detecting and reusing duplicate content
        services.AddScoped<IContentDeduplicationService, ContentDeduplicationService>();

        // Register employee language change handler (triggers translation for new-to-tenant languages)
        services.AddScoped<IEmployeeLanguageChangeHandler, EmployeeLanguageChangeHandler>();

        // Register AI slideshow generation service (Claude API for HTML slideshow from PDF)
        services.AddHttpClient<IAiSlideshowGenerationService, AiSlideshowGenerationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for PDF analysis and HTML generation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<AiSlideshowGenerationService>>()));

        // Register slideshow generation service (orchestrates PDF download + AI generation)
        services.AddHttpClient<ISlideshowGenerationService, SlideshowGenerationService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30); // 30 seconds for PDF download
        });

        // Register translation validation configuration
        services.Configure<TranslationValidationSettings>(
            configuration.GetSection(TranslationValidationSettings.SectionName));

        // Register translation validation back-translation providers
        // DeepL: direct REST API, typically fastest for European languages
        services.AddHttpClient<IDeepLTranslationService, DeepLTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2); // 2 minutes for back-translation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetTransientPolicy(
            sp.GetRequiredService<ILogger<DeepLTranslationService>>(), "DeepL"));
        // Gemini: Google AI, good for diverse language pairs
        services.AddHttpClient<IGeminiTranslationService, GeminiTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2); // 2 minutes for back-translation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetTransientPolicy(
            sp.GetRequiredService<ILogger<GeminiTranslationService>>(), "Gemini"));
        // DeepSeek: OpenAI-compatible API, configurable base URL
        services.AddHttpClient<IDeepSeekTranslationService, DeepSeekTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2); // 2 minutes for back-translation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetTransientPolicy(
            sp.GetRequiredService<ILogger<DeepSeekTranslationService>>(), "DeepSeek"));

        // Register translation validation scoring and diff services (pure logic, no HTTP)
        services.AddSingleton<ILexicalScoringService, LexicalScoringService>();
        services.AddSingleton<IWordDiffService, WordDiffService>();

        // Register dialect detection service (uses Claude Haiku API)
        services.AddHttpClient<IDialectDetectionService, DialectDetectionService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(1); // 1 minute for dialect detection
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<DialectDetectionService>>()));

        // Register safety classification and glossary verification services (scoped — cache per request)
        services.AddScoped<ISafetyClassificationService, SafetyClassificationService>();
        services.AddScoped<IGlossaryTermVerificationService, GlossaryTermVerificationService>();

        // Register Claude Haiku back-translation service (Provider A in consensus engine)
        services.AddHttpClient<IClaudeHaikuBackTranslationService, ClaudeHaikuBackTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2); // 2 minutes for back-translation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<ClaudeHaikuBackTranslationService>>()));

        // Register consensus engine (multi-round back-translation scoring)
        services.AddScoped<IConsensusEngine, ConsensusEngine>();

        // Register translation validation orchestrator (single-section validation pipeline)
        services.AddScoped<ITranslationValidationService, TranslationValidationService>();

        // Register validation report PDF generation service
        services.AddScoped<IValidationReportService, ValidationReportService>();

        // Register regulatory score service (Claude Sonnet for regulatory scoring)
        services.AddHttpClient<IRegulatoryScoreService, RegulatoryScoreService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3); // 3 minutes for regulatory scoring
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<RegulatoryScoreService>>()));

        // Register sector services (system-wide sector lookup + tenant-sector management)
        services.AddScoped<ISectorService, SectorService>();
        services.AddScoped<ITenantSectorService, TenantSectorService>();

        // Register content creation session services (Phase 7 — creation wizard pipeline)
        services.AddHttpClient<IContentParserService, ContentParserService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3); // 3 minutes for content parsing
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<ContentParserService>>()));
        services.AddScoped<IContentCreationSessionService, ContentCreationSessionService>();

        // Register requirement ingestion service (AI-powered regulatory requirement extraction)
        services.AddScoped<IRequirementIngestionService, RequirementIngestionService>();

        // Register requirement ingestion job HttpClient (fetches web pages + calls Claude API)
        services.AddHttpClient<RequirementIngestionJob>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for document fetch + AI extraction
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<RequirementIngestionJob>>()));

        // Register requirement mapping service (AI-suggested requirement ↔ content mappings)
        services.AddScoped<IRequirementMappingService, RequirementMappingService>();

        // Register inspection readiness report service (QuestPDF generation + R2 storage)
        services.AddScoped<IInspectionReportService, InspectionReportService>();

        // Register requirement mapping job HttpClient (calls Claude API for content analysis)
        services.AddHttpClient<RequirementMappingJob>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for AI mapping analysis
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<RequirementMappingJob>>()));

        // Note: SignalR hubs are registered in Program.cs with app.MapHub<>()
        //   - SubtitleProcessingHub: /api/hubs/subtitle-processing
        //   - ContentGenerationHub: /api/hubs/content-generation
        //   - TranslationValidationHub: /api/hubs/translation-validation
        // Note: Hangfire background jobs (ContentGenerationJob, TranslationValidationJob, etc.) are registered in Program.cs

        return services;
    }
}
