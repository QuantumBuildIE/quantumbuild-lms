using Hangfire;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateContentTranslations;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Hangfire background job that detects and generates translations for languages
/// that are missing after content reuse. When content is copied from a source talk
/// that was created before new languages were added, this job fills the gap.
///
/// Handles three types of missing translations:
/// 1. Content translations (sections, quiz, slideshow HTML) via GenerateContentTranslationsCommand
/// 2. Video subtitle translations via SubtitleProcessingOrchestrator
/// </summary>
public class MissingTranslationsJob
{
    private readonly ICoreDbContext _coreDbContext;
    private readonly IToolboxTalksDbContext _toolboxTalksDbContext;
    private readonly ISender _sender;
    private readonly ILanguageCodeService _languageCodeService;
    private readonly ISubtitleProcessingOrchestrator _subtitleOrchestrator;
    private readonly IHubContext<ContentGenerationHub> _hubContext;
    private readonly ILogger<MissingTranslationsJob> _logger;

    public MissingTranslationsJob(
        ICoreDbContext coreDbContext,
        IToolboxTalksDbContext toolboxTalksDbContext,
        ISender sender,
        ILanguageCodeService languageCodeService,
        ISubtitleProcessingOrchestrator subtitleOrchestrator,
        IHubContext<ContentGenerationHub> hubContext,
        ILogger<MissingTranslationsJob> logger)
    {
        _coreDbContext = coreDbContext;
        _toolboxTalksDbContext = toolboxTalksDbContext;
        _sender = sender;
        _languageCodeService = languageCodeService;
        _subtitleOrchestrator = subtitleOrchestrator;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Detects missing translations and generates them for a toolbox talk.
    /// Compares existing translations against current employee language preferences
    /// and generates only what's missing.
    /// </summary>
    [AutomaticRetry(Attempts = 1)]
    [Queue("content-generation")]
    public async Task ExecuteAsync(
        Guid toolboxTalkId,
        Guid tenantId,
        string? connectionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "MissingTranslationsJob started for ToolboxTalk {ToolboxTalkId}, TenantId {TenantId}",
            toolboxTalkId, tenantId);

        try
        {
            // Get the source language of the toolbox talk
            var sourceLanguageCode = await _toolboxTalksDbContext.ToolboxTalks
                .IgnoreQueryFilters()
                .Where(t => t.Id == toolboxTalkId && t.TenantId == tenantId && !t.IsDeleted)
                .Select(t => t.SourceLanguageCode)
                .FirstOrDefaultAsync(cancellationToken) ?? "en";

            // Get existing translation language codes for this talk
            var existingLanguageCodes = await _toolboxTalksDbContext.ToolboxTalkTranslations
                .IgnoreQueryFilters()
                .Where(t => t.ToolboxTalkId == toolboxTalkId && t.TenantId == tenantId && !t.IsDeleted)
                .Select(t => t.LanguageCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "ToolboxTalk {ToolboxTalkId} has existing translations for: {ExistingLanguages}",
                toolboxTalkId, string.Join(", ", existingLanguageCodes));

            // Get all required languages from employee preferences (excluding source language)
            var requiredLanguageCodes = await _coreDbContext.Employees
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId && !e.IsDeleted
                    && e.PreferredLanguage != null && e.PreferredLanguage != sourceLanguageCode)
                .Select(e => e.PreferredLanguage!)
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Required languages for tenant {TenantId} (excluding source '{Source}'): {RequiredLanguages}",
                tenantId, sourceLanguageCode, string.Join(", ", requiredLanguageCodes));

            // Compute missing languages
            var missingLanguageCodes = requiredLanguageCodes
                .Except(existingLanguageCodes, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingLanguageCodes.Count == 0)
            {
                _logger.LogInformation(
                    "No missing content translations for ToolboxTalk {ToolboxTalkId}. All required languages are covered.",
                    toolboxTalkId);
            }
            else
            {
                // Generate missing content translations (sections, quiz, slideshow HTML)
                await GenerateMissingContentTranslationsAsync(
                    toolboxTalkId, tenantId, connectionId, missingLanguageCodes, cancellationToken);
            }

            // Generate missing subtitle translations (independent from content translations)
            // This checks the subtitle processing job's translations, not ToolboxTalkTranslation records
            await GenerateMissingSubtitleTranslationsAsync(
                toolboxTalkId, tenantId, connectionId, requiredLanguageCodes, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "MissingTranslationsJob failed for ToolboxTalk {ToolboxTalkId}",
                toolboxTalkId);

            try
            {
                await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                    "Translating", 100,
                    "Translation generation encountered an error. Translations can be generated manually.");
            }
            catch
            {
                // Swallow - we're already in error handling
            }

            throw;
        }
    }

    /// <summary>
    /// Generates missing content translations (sections, quiz, slides, slideshow HTML)
    /// by dispatching GenerateContentTranslationsCommand.
    /// </summary>
    private async Task GenerateMissingContentTranslationsAsync(
        Guid toolboxTalkId,
        Guid tenantId,
        string? connectionId,
        List<string> missingLanguageCodes,
        CancellationToken cancellationToken)
    {
        // Convert codes to language names
        var missingLanguageNames = new List<string>();
        foreach (var code in missingLanguageCodes)
        {
            missingLanguageNames.Add(await _languageCodeService.GetLanguageNameAsync(code));
        }

        _logger.LogInformation(
            "Generating missing content translations for ToolboxTalk {ToolboxTalkId}: {MissingLanguages}",
            toolboxTalkId, string.Join(", ", missingLanguageNames));

        await SendProgressUpdateAsync(toolboxTalkId, connectionId,
            "Translating", 50,
            $"Generating {missingLanguageNames.Count} missing content translation(s)...");

        var command = new GenerateContentTranslationsCommand
        {
            ToolboxTalkId = toolboxTalkId,
            TenantId = tenantId,
            TargetLanguages = missingLanguageNames
        };

        var result = await _sender.Send(command, cancellationToken);

        if (result.Success)
        {
            var successCount = result.LanguageResults.Count(r => r.Success);
            var totalCount = result.LanguageResults.Count;

            _logger.LogInformation(
                "Missing content translations generated for ToolboxTalk {ToolboxTalkId}: {SuccessCount}/{TotalCount}",
                toolboxTalkId, successCount, totalCount);

            foreach (var langResult in result.LanguageResults)
            {
                _logger.LogInformation(
                    "Content translation result for {Language} ({Code}): Success={Success}, " +
                    "Sections={Sections}, Questions={Questions}, Slides={Slides}, Slideshow={Slideshow}, Error={Error}",
                    langResult.Language, langResult.LanguageCode, langResult.Success,
                    langResult.SectionsTranslated, langResult.QuestionsTranslated,
                    langResult.SlidesTranslated, langResult.SlideshowTranslated,
                    langResult.ErrorMessage ?? "none");
            }

            await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                "Translating", 80,
                $"Content translations complete ({successCount}/{totalCount} languages).");
        }
        else
        {
            _logger.LogWarning(
                "Missing content translations generation failed for ToolboxTalk {ToolboxTalkId}: {Error}",
                toolboxTalkId, result.ErrorMessage);

            await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                "Translating", 80,
                "Some content translations could not be generated. They can be generated manually.");
        }
    }

    /// <summary>
    /// Generates missing subtitle translations by checking existing subtitle processing jobs
    /// and translating to any languages that don't have subtitle translations yet.
    /// </summary>
    private async Task GenerateMissingSubtitleTranslationsAsync(
        Guid toolboxTalkId,
        Guid tenantId,
        string? connectionId,
        List<string> requiredLanguageCodes,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Checking for missing subtitle translations for ToolboxTalk {ToolboxTalkId}",
                toolboxTalkId);

            await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                "Translating", 85,
                "Checking for missing subtitle translations...");

            var translatedCount = await _subtitleOrchestrator.TranslateMissingLanguagesAsync(
                toolboxTalkId, tenantId, requiredLanguageCodes, cancellationToken);

            if (translatedCount > 0)
            {
                _logger.LogInformation(
                    "Generated {Count} missing subtitle translation(s) for ToolboxTalk {ToolboxTalkId}",
                    translatedCount, toolboxTalkId);

                await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                    "Translating", 100,
                    $"All translations complete. Generated {translatedCount} missing subtitle translation(s).");
            }
            else
            {
                _logger.LogInformation(
                    "No missing subtitle translations for ToolboxTalk {ToolboxTalkId}",
                    toolboxTalkId);

                await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                    "Translating", 100,
                    "All translations complete.");
            }
        }
        catch (Exception ex)
        {
            // Subtitle translation failure should not fail the entire job
            _logger.LogError(ex,
                "Failed to generate missing subtitle translations for ToolboxTalk {ToolboxTalkId}. " +
                "Subtitle translations can be generated manually.",
                toolboxTalkId);

            await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                "Translating", 100,
                "Content translations complete. Subtitle translations may need to be generated manually.");
        }
    }

    private async Task SendProgressUpdateAsync(
        Guid toolboxTalkId,
        string? connectionId,
        string stage,
        int percentComplete,
        string message)
    {
        try
        {
            var payload = new
            {
                toolboxTalkId,
                stage,
                percentComplete,
                message
            };

            if (!string.IsNullOrEmpty(connectionId))
            {
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("ContentGenerationProgress", payload);
            }

            await _hubContext.Clients.Group($"content-generation-{toolboxTalkId}")
                .SendAsync("ContentGenerationProgress", payload);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send progress update for toolbox talk {ToolboxTalkId}",
                toolboxTalkId);
        }
    }
}
