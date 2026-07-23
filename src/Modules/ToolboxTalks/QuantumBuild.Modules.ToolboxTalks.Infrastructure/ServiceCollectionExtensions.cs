using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
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
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ArtefactScan;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Mapping;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.PreFlightScan;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.SafetyTermRegistry;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Sectors;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Standards;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Reviewers;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Mapping;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Sectors;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Standards;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Reviewers;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Ingestion;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;
using Polly;
using QuantumBuild.Core.Application.Http;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure;

/// <summary>
/// Dependency injection configuration for the Toolbox Talks Infrastructure layer
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Resolves the shared Anthropic Bulkhead policy instance from DI. Used by every
    /// Claude-calling HttpClient registration so they all draw from ONE shared permit pool
    /// (see ProviderBulkheadPolicies — one instance per provider, not per typed client).
    /// </summary>
    private static IAsyncPolicy<HttpResponseMessage> GetAnthropicBulkheadFromDI(IServiceProvider sp) =>
        sp.GetRequiredService<ProviderBulkheadPolicies>().Anthropic;

    /// <summary>Shared DeepL Bulkhead policy instance from DI (see GetAnthropicBulkheadFromDI).</summary>
    private static IAsyncPolicy<HttpResponseMessage> GetDeepLBulkheadFromDI(IServiceProvider sp) =>
        sp.GetRequiredService<ProviderBulkheadPolicies>().DeepL;

    /// <summary>Shared Gemini Bulkhead policy instance from DI (see GetAnthropicBulkheadFromDI).</summary>
    private static IAsyncPolicy<HttpResponseMessage> GetGeminiBulkheadFromDI(IServiceProvider sp) =>
        sp.GetRequiredService<ProviderBulkheadPolicies>().Gemini;

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

        // Register translation/validation pipeline notification service
        services.AddScoped<IToolboxTalkNotificationService, ToolboxTalkNotificationService>();

        // Register reports service
        services.AddScoped<IToolboxTalkReportsService, ToolboxTalkReportsService>();

        // Register quiz generation service (question randomization and shuffling)
        services.AddScoped<IQuizGenerationService, QuizGenerationService>();

        // Register export service (stub implementation for Phase 2)
        services.AddScoped<IToolboxTalkExportService, ToolboxTalkExportService>();

        // Register subtitle processing configuration with startup validation
        services.AddOptions<SubtitleProcessingSettings>()
            .BindConfiguration(SubtitleProcessingSettings.SectionName)
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SubtitleProcessingSettings>, SubtitleProcessingSettingsValidator>();

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

        // Register DOCX extraction service (for AI content generation from uploaded Word documents)
        services.AddHttpClient<IDocxExtractionService, DocxExtractionService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30); // 30 seconds for DOCX download
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
        // (bundled hygiene fix: this registration previously had NO retry policy at all,
        // unlike every other Claude-calling service — added here alongside the bulkhead)
        services.AddHttpClient<ITranslationService, ClaudeTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for translation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<ClaudeTranslationService>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

        // Content translation service for translating sections and quiz questions
        services.AddHttpClient<IContentTranslationService, ContentTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for content translation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<ContentTranslationService>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

        // Translation job scheduler (fire-and-forget Hangfire enqueue, used by cross-module callers)
        services.AddSingleton<ITranslationJobScheduler, TranslationJobScheduler>();

        // Parse job scheduler (fire-and-forget Hangfire enqueue for new-wizard video parse pipeline)
        services.AddSingleton<IParseJobScheduler, ParseJobScheduler>();

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
            sp.GetRequiredService<ILogger<AiSectionGenerationService>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

        // Register AI quiz generation service for generating quiz questions from content
        services.AddHttpClient<IAiQuizGenerationService, AiQuizGenerationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3); // 3 minutes for quiz generation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<AiQuizGenerationService>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

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
            sp.GetRequiredService<ILogger<AiSlideshowGenerationService>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

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
            sp.GetRequiredService<ILogger<DeepLTranslationService>>(), "DeepL"))
        .AddPolicyHandler((sp, _) => GetDeepLBulkheadFromDI(sp));
        // Gemini: Google AI, good for diverse language pairs
        services.AddHttpClient<IGeminiTranslationService, GeminiTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2); // 2 minutes for back-translation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetTransientPolicy(
            sp.GetRequiredService<ILogger<GeminiTranslationService>>(), "Gemini"))
        .AddPolicyHandler((sp, _) => GetGeminiBulkheadFromDI(sp));
        // Claude Sonnet: Round 3 final tiebreaker (v6.4 — replaced DeepSeek for GDPR compliance)
        services.AddHttpClient<IClaudeSonnetBackTranslationService, ClaudeSonnetBackTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2); // 2 minutes for back-translation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<ClaudeSonnetBackTranslationService>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

        // Register translation validation scoring and diff services (pure logic, no HTTP)
        services.AddSingleton<ILexicalScoringService, LexicalScoringService>();
        services.AddSingleton<IWordDiffService, WordDiffService>();
        services.AddSingleton<ISentenceSplitter, SentenceSplitter>();
        services.AddSingleton<IBackTranslationSelector, BackTranslationSelector>();
        services.AddSingleton<IDiffRunGrouper, DiffRunGrouper>();
        services.AddSingleton<IWordToSentenceMapper, WordToSentenceMapper>();

        // Register dialect detection service (uses Claude Haiku API)
        services.AddHttpClient<IDialectDetectionService, DialectDetectionService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(1); // 1 minute for dialect detection
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<DialectDetectionService>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

        // Register safety classification, glossary verification, and glossary hard-block replacement
        services.AddScoped<ISafetyClassificationService, SafetyClassificationService>();
        services.AddScoped<IGlossaryTermVerificationService, GlossaryTermVerificationService>();
        services.AddScoped<IGlossaryReplacementService, GlossaryReplacementService>();

        // Register Claude Haiku back-translation service (Provider A in consensus engine)
        services.AddHttpClient<IClaudeHaikuBackTranslationService, ClaudeHaikuBackTranslationService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2); // 2 minutes for back-translation
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<ClaudeHaikuBackTranslationService>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

        // Register consensus engine (multi-round back-translation scoring)
        services.AddScoped<IConsensusEngine, ConsensusEngine>();

        // Register translation validation orchestrator (single-section validation pipeline)
        services.AddScoped<ITranslationValidationService, TranslationValidationService>();

        // Register validation report PDF generation service
        services.AddScoped<IValidationReportService, ValidationReportService>();

        // Register artefact scan service for translation quality checks
        services.AddScoped<IArtefactScanService, ArtefactScanService>();

        // Register safety term registry for known-bad translation pattern detection
        services.AddScoped<ISafetyTermRegistryService, SafetyTermRegistryService>();

        // Register pre-flight scan service (Claude Haiku for source content analysis before translation)
        services.AddHttpClient<IPreFlightScanService, PreFlightScanService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(1);
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<PreFlightScanService>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

        // Register regulatory score service (Claude Sonnet for regulatory scoring).
        // Sole consumer is RegulatoryScoreController's synchronous request-path action (confirmed —
        // no Hangfire job references IRegulatoryScoreService) — so this gets the AnthropicSynchronous
        // bulkhead variant (shared Anthropic permit pool + outer Timeout) instead of the plain
        // Anthropic bulkhead every other Claude-calling registration uses. This makes a live admin
        // request fail fast with a timeout rather than hang indefinitely if background jobs have
        // saturated the shared Anthropic quota.
        services.AddHttpClient<IRegulatoryScoreService, RegulatoryScoreService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3); // 3 minutes for regulatory scoring
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<RegulatoryScoreService>>()))
        .AddPolicyHandler((sp, _) => sp.GetRequiredService<ProviderBulkheadPolicies>().AnthropicSynchronous);

        // Register sector services (system-wide sector lookup + tenant-sector management)
        services.AddScoped<ISectorService, SectorService>();
        services.AddScoped<ITenantSectorService, TenantSectorService>();
        services.AddScoped<ITenantStandardSubscriptionService, TenantStandardSubscriptionService>();

        // Register tenant reviewer configuration service (per-language external reviewer config)
        services.AddScoped<ITenantReviewerConfigurationService, TenantReviewerConfigurationService>();

        // Register content creation session services (Phase 7 — creation wizard pipeline)
        services.AddHttpClient<IContentParserService, ContentParserService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3); // 3 minutes for content parsing
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<ContentParserService>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));
        services.AddScoped<IContentCreationSessionService, ContentCreationSessionService>();

        // Register requirement ingestion service (AI-powered regulatory requirement extraction)
        services.AddScoped<IRequirementIngestionService, RequirementIngestionService>();

        // Register requirement ingestion job HttpClient (fetches web pages + calls Claude API)
        services.AddHttpClient<RequirementIngestionJob>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for document fetch + AI extraction
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<RequirementIngestionJob>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

        // Register requirement mapping service (AI-suggested requirement ↔ content mappings)
        services.AddScoped<IRequirementMappingService, RequirementMappingService>();

        // Register inspection readiness report service (QuestPDF generation + R2 storage)
        services.AddScoped<IInspectionReportService, InspectionReportService>();

        // Register AI usage logger (fire-and-forget token tracking per API call)
        services.AddScoped<IAiUsageLogger, AiUsageLogger>();

        // Register pipeline version service (system-level audit of active translation pipeline config)
        services.AddScoped<IPipelineVersionService, PipelineVersionService>();

        // Register pipeline audit Phase 2 services
        services.AddScoped<ITranslationDeviationService, TranslationDeviationService>();
        services.AddScoped<IPipelineAuditQueryService, PipelineAuditQueryService>();

        // Register pipeline audit Phase 4 services — Corpus
        services.AddScoped<ICostEstimationService, CostEstimationService>();
        services.AddScoped<IAuditCorpusService, AuditCorpusService>();
        services.AddScoped<CorpusRunJob>();

        // Register requirement mapping job HttpClient (calls Claude API for content analysis)
        services.AddHttpClient<RequirementMappingJob>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5); // 5 minutes for AI mapping analysis
        })
        .AddPolicyHandler((sp, _) => ResiliencePolicies.GetClaudePolicy(
            sp.GetRequiredService<ILogger<RequirementMappingJob>>()))
        .AddPolicyHandler((sp, _) => GetAnthropicBulkheadFromDI(sp));

        // Register translation workflow service (Phase 1b — event log, no state machine enforcement yet)
        services.AddScoped<ITranslationWorkflowService, TranslationWorkflowService>();

        // Note: SignalR hubs are registered in Program.cs with app.MapHub<>()
        //   - SubtitleProcessingHub: /api/hubs/subtitle-processing
        //   - ContentGenerationHub: /api/hubs/content-generation
        //   - TranslationValidationHub: /api/hubs/translation-validation
        // Note: Hangfire background jobs (ContentGenerationJob, TranslationValidationJob, etc.) are registered in Program.cs

        return services;
    }
}
