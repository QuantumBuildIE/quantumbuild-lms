using System.Diagnostics;
using System.Text.Json;
using Hangfire;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateContentTranslations;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Hangfire background job for orchestrating AI content generation for toolbox talks.
/// This job extracts content from video/PDF, generates sections and quiz questions using AI,
/// and reports real-time progress via SignalR.
/// After successful content generation, automatically generates translations for all
/// languages used by employees in the tenant.
/// </summary>
public class ContentGenerationJob
{
    private readonly IContentGenerationService _generationService;
    private readonly IHubContext<ContentGenerationHub> _hubContext;
    private readonly ICoreDbContext _coreDbContext;
    private readonly IToolboxTalksDbContext _toolboxTalksDbContext;
    private readonly ISender _sender;
    private readonly ILanguageCodeService _languageCodeService;
    private readonly ISlideshowGenerationService _slideshowGenerationService;
    private readonly ILogger<ContentGenerationJob> _logger;

    public ContentGenerationJob(
        IContentGenerationService generationService,
        IHubContext<ContentGenerationHub> hubContext,
        ICoreDbContext coreDbContext,
        IToolboxTalksDbContext toolboxTalksDbContext,
        ISender sender,
        ILanguageCodeService languageCodeService,
        ISlideshowGenerationService slideshowGenerationService,
        ILogger<ContentGenerationJob> logger)
    {
        _generationService = generationService;
        _hubContext = hubContext;
        _coreDbContext = coreDbContext;
        _toolboxTalksDbContext = toolboxTalksDbContext;
        _sender = sender;
        _languageCodeService = languageCodeService;
        _slideshowGenerationService = slideshowGenerationService;
        _logger = logger;
    }

    /// <summary>
    /// Executes the content generation job.
    /// </summary>
    /// <param name="toolboxTalkId">The toolbox talk to generate content for</param>
    /// <param name="options">Generation options</param>
    /// <param name="connectionId">Optional SignalR connection ID for direct client updates</param>
    /// <param name="tenantId">The tenant ID (passed explicitly since Hangfire jobs run outside HTTP context)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [AutomaticRetry(Attempts = 1)] // Don't retry AI generation - it's expensive
    [Queue("content-generation")] // Use dedicated queue for resource-intensive jobs
    public async Task ExecuteAsync(
        Guid toolboxTalkId,
        ContentGenerationOptions options,
        string? connectionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "========== CONTENT GENERATION JOB STARTED ==========\n" +
            "ToolboxTalkId: {ToolboxTalkId}\n" +
            "TenantId: {TenantId}\n" +
            "ConnectionId: {ConnectionId}\n" +
            "Options: IncludeVideo={IncludeVideo}, IncludePdf={IncludePdf}, " +
            "MinSections={MinSections}, MinQuestions={MinQuestions}, " +
            "PassThreshold={PassThreshold}, ReplaceExisting={ReplaceExisting}",
            toolboxTalkId,
            tenantId,
            connectionId ?? "none",
            options.IncludeVideo,
            options.IncludePdf,
            options.MinimumSections,
            options.MinimumQuestions,
            options.PassThreshold,
            options.ReplaceExisting);

        ContentGenerationResult? result = null;

        try
        {
            // Save source language and slideshow settings to the entity BEFORE generation
            // so auto-translation and auto-slideshow steps can read them from the DB
            var talk = await _toolboxTalksDbContext.ToolboxTalks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == toolboxTalkId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);

            if (talk != null)
            {
                talk.SourceLanguageCode = options.SourceLanguageCode;
                talk.GenerateSlidesFromPdf = options.GenerateSlidesFromPdf;
                var saved = await _toolboxTalksDbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation(
                    "Saved generation settings for ToolboxTalk {ToolboxTalkId}: SourceLanguageCode={SourceLang}, GenerateSlidesFromPdf={GenerateSlides}, RowsSaved={Rows}",
                    toolboxTalkId, options.SourceLanguageCode, options.GenerateSlidesFromPdf, saved);
            }

            // Create progress reporter that sends updates via SignalR
            var progress = new Progress<ContentGenerationProgress>(async update =>
            {
                _logger.LogInformation(
                    "[Progress] ToolboxTalk {ToolboxTalkId}: Stage={Stage}, Percent={Percent}%, Message={Message}",
                    toolboxTalkId, update.Stage, update.PercentComplete, update.Message);

                await SendProgressUpdateAsync(toolboxTalkId, connectionId, update);
            });

            _logger.LogInformation(
                "[Step 1/4] Starting content extraction for toolbox talk {ToolboxTalkId}...",
                toolboxTalkId);

            // Execute the content generation
            result = await _generationService.GenerateContentAsync(
                toolboxTalkId,
                options,
                tenantId,
                progress,
                cancellationToken);

            stopwatch.Stop();

            // Auto-generate translations for employee languages if content generation succeeded
            if (result.Success)
            {
                _logger.LogInformation(
                    "[DEBUG] Content generation complete for ToolboxTalk {ToolboxTalkId}. " +
                    "Sections created: {SectionCount}, Questions created: {QuestionCount}, " +
                    "HasFinalPortionQuestion: {HasFinalQuestion}. Now starting auto-translation...",
                    toolboxTalkId, result.SectionsGenerated, result.QuestionsGenerated, result.HasFinalPortionQuestion);

                // Generate slideshow if enabled
                await AutoGenerateSlidesAsync(toolboxTalkId, tenantId, connectionId, options.SlideshowSource, cancellationToken);

                await AutoGenerateTranslationsAsync(toolboxTalkId, tenantId, connectionId, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "[DEBUG] Content generation FAILED for ToolboxTalk {ToolboxTalkId}. " +
                    "Skipping auto-translation. Errors: {Errors}",
                    toolboxTalkId, string.Join("; ", result.Errors));
            }

            // Send completion notification
            await SendCompletionNotificationAsync(toolboxTalkId, connectionId, result);

            if (result.Success)
            {
                var successType = result.PartialSuccess ? "WITH WARNINGS" : "SUCCESSFULLY";
                _logger.LogInformation(
                    "========== CONTENT GENERATION JOB COMPLETED {SuccessType} ==========\n" +
                    "ToolboxTalkId: {ToolboxTalkId}\n" +
                    "Duration: {Duration}ms ({DurationSeconds:F1}s)\n" +
                    "Partial Success: {PartialSuccess}\n" +
                    "Sections Generated: {Sections}\n" +
                    "Questions Generated: {Questions}\n" +
                    "Has Final Portion Question: {HasFinalQuestion}\n" +
                    "Total Tokens Used: {Tokens}\n" +
                    "Warnings: {WarningCount}",
                    successType,
                    toolboxTalkId,
                    stopwatch.ElapsedMilliseconds,
                    stopwatch.ElapsedMilliseconds / 1000.0,
                    result.PartialSuccess,
                    result.SectionsGenerated,
                    result.QuestionsGenerated,
                    result.HasFinalPortionQuestion,
                    result.TotalTokensUsed,
                    result.Warnings.Count);

                if (result.Warnings.Count > 0)
                {
                    _logger.LogWarning(
                        "Content generation warnings for toolbox talk {ToolboxTalkId}: {Warnings}",
                        toolboxTalkId, string.Join("; ", result.Warnings));
                }
            }
            else
            {
                _logger.LogError(
                    "========== CONTENT GENERATION JOB FAILED ==========\n" +
                    "ToolboxTalkId: {ToolboxTalkId}\n" +
                    "Duration: {Duration}ms ({DurationSeconds:F1}s)\n" +
                    "Sections Generated: {Sections}\n" +
                    "Questions Generated: {Questions}\n" +
                    "Errors: {Errors}",
                    toolboxTalkId,
                    stopwatch.ElapsedMilliseconds,
                    stopwatch.ElapsedMilliseconds / 1000.0,
                    result.SectionsGenerated,
                    result.QuestionsGenerated,
                    string.Join("; ", result.Errors));
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "========== CONTENT GENERATION JOB CANCELLED ==========\n" +
                "ToolboxTalkId: {ToolboxTalkId}\n" +
                "Duration before cancellation: {Duration}ms",
                toolboxTalkId, stopwatch.ElapsedMilliseconds);

            // Send failure notification to client
            await SendCompletionNotificationAsync(toolboxTalkId, connectionId, new ContentGenerationResult(
                Success: false,
                PartialSuccess: false,
                SectionsGenerated: 0,
                QuestionsGenerated: 0,
                HasFinalPortionQuestion: false,
                Errors: new List<string> { "Content generation was cancelled" },
                Warnings: new List<string>(),
                TotalTokensUsed: 0));

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "========== CONTENT GENERATION JOB EXCEPTION ==========\n" +
                "ToolboxTalkId: {ToolboxTalkId}\n" +
                "Duration before error: {Duration}ms\n" +
                "Exception Type: {ExceptionType}\n" +
                "Exception Message: {ExceptionMessage}\n" +
                "Stack Trace: {StackTrace}",
                toolboxTalkId,
                stopwatch.ElapsedMilliseconds,
                ex.GetType().FullName,
                ex.Message,
                ex.StackTrace);

            // Send failure notification to client
            await SendCompletionNotificationAsync(toolboxTalkId, connectionId, new ContentGenerationResult(
                Success: false,
                PartialSuccess: false,
                SectionsGenerated: result?.SectionsGenerated ?? 0,
                QuestionsGenerated: result?.QuestionsGenerated ?? 0,
                HasFinalPortionQuestion: result?.HasFinalPortionQuestion ?? false,
                Errors: new List<string> { "An unexpected error occurred. Please try again." },
                Warnings: result?.Warnings ?? new List<string>(),
                TotalTokensUsed: result?.TotalTokensUsed ?? 0));

            throw;
        }
    }

    /// <summary>
    /// Generates a user-friendly completion message based on the result.
    /// </summary>
    private static string GetCompletionMessage(ContentGenerationResult result)
    {
        if (!result.Success)
        {
            return "Content generation failed";
        }

        if (result.PartialSuccess)
        {
            return $"Content generated with some limitations ({result.SectionsGenerated} sections, {result.QuestionsGenerated} questions)";
        }

        return $"Content generated successfully ({result.SectionsGenerated} sections, {result.QuestionsGenerated} questions)";
    }

    /// <summary>
    /// Automatically generates slideshow slides from the PDF if the toolbox talk has
    /// GenerateSlidesFromPdf enabled and has a PDF uploaded. Failures are logged
    /// but do not affect the overall content generation result.
    /// </summary>
    private async Task AutoGenerateSlidesAsync(
        Guid toolboxTalkId,
        Guid tenantId,
        string? connectionId,
        string slideshowSource = "none",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Determine effective source: use explicit slideshowSource if provided,
            // otherwise fall back to GenerateSlidesFromPdf flag on entity
            var effectiveSource = slideshowSource;
            if (string.Equals(effectiveSource, "none", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(effectiveSource))
            {
                var talk = await _toolboxTalksDbContext.ToolboxTalks
                    .IgnoreQueryFilters()
                    .Where(t => t.Id == toolboxTalkId && t.TenantId == tenantId && !t.IsDeleted)
                    .Select(t => new { t.GenerateSlidesFromPdf, t.PdfUrl })
                    .FirstOrDefaultAsync(cancellationToken);

                if (talk == null || !talk.GenerateSlidesFromPdf || string.IsNullOrEmpty(talk.PdfUrl))
                {
                    _logger.LogInformation(
                        "Slideshow generation skipped for ToolboxTalk {ToolboxTalkId}. " +
                        "SlideshowSource={Source}, GenerateSlidesFromPdf={Enabled}, HasPdf={HasPdf}",
                        toolboxTalkId,
                        slideshowSource,
                        talk?.GenerateSlidesFromPdf ?? false,
                        !string.IsNullOrEmpty(talk?.PdfUrl));
                    return;
                }

                effectiveSource = "pdf"; // Legacy fallback
            }

            _logger.LogInformation(
                "Auto-generating slideshow for ToolboxTalk {ToolboxTalkId} from source '{Source}'...",
                toolboxTalkId, effectiveSource);

            await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                new ContentGenerationProgress(
                    "GeneratingSlides", 90,
                    $"Generating slideshow from {effectiveSource}..."));

            var slideResult = await _slideshowGenerationService.GenerateSlideshowAsync(
                tenantId, toolboxTalkId, effectiveSource, cancellationToken);

            if (slideResult.Success && !string.IsNullOrEmpty(slideResult.Data))
            {
                _logger.LogInformation(
                    "AI slideshow generated for ToolboxTalk {ToolboxTalkId}: {HtmlLength} chars",
                    toolboxTalkId, slideResult.Data.Length);

                await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                    new ContentGenerationProgress(
                        "GeneratingSlides", 92,
                        "AI slideshow generated successfully."));
            }
            else
            {
                var errorDetail = string.Join("; ", slideResult.Errors);

                _logger.LogWarning(
                    "Slideshow generation failed for ToolboxTalk {ToolboxTalkId}: {Errors}",
                    toolboxTalkId, errorDetail);

                await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                    new ContentGenerationProgress(
                        "GeneratingSlides", 92,
                        "Slideshow generation failed. Slides can be generated manually from the edit page."));
            }
        }
        catch (Exception ex)
        {
            // Slideshow failure should never fail the content generation job
            _logger.LogError(ex,
                "Slideshow generation failed unexpectedly for ToolboxTalk {ToolboxTalkId}. " +
                "Content generation was successful. Slides can be generated manually.",
                toolboxTalkId);

            try
            {
                await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                    new ContentGenerationProgress(
                        "GeneratingSlides", 92,
                        "Auto-slideshow generation skipped due to an error. Slides can be generated manually."));
            }
            catch
            {
                // Swallow - we're already in error handling
            }
        }
    }

    /// <summary>
    /// Automatically generates content translations for all non-English languages
    /// used by employees in the tenant. Failures are logged but do not affect
    /// the overall content generation result.
    /// </summary>
    private async Task AutoGenerateTranslationsAsync(
        Guid toolboxTalkId,
        Guid tenantId,
        string? connectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "[DEBUG] AutoGenerateTranslationsAsync called for ToolboxTalk {ToolboxTalkId}, TenantId {TenantId}",
                toolboxTalkId, tenantId);

            // Get the source language of the toolbox talk
            var sourceLanguageCode = await _toolboxTalksDbContext.ToolboxTalks
                .IgnoreQueryFilters()
                .Where(t => t.Id == toolboxTalkId && t.TenantId == tenantId && !t.IsDeleted)
                .Select(t => t.SourceLanguageCode)
                .FirstOrDefaultAsync(cancellationToken) ?? "en";

            _logger.LogInformation(
                "[DEBUG] ToolboxTalk {ToolboxTalkId} source language: {SourceLanguage}",
                toolboxTalkId, sourceLanguageCode);

            // NOTE: We use IgnoreQueryFilters() because this method runs in a background job context
            // where ICurrentUserService.TenantId may not be set correctly for the global tenant filter.
            // Since we explicitly filter by tenantId and IsDeleted, this is safe.
            // Filter out employees whose preferred language matches the source language
            var employeeLanguageCodes = await _coreDbContext.Employees
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId && !e.IsDeleted
                    && e.PreferredLanguage != null && e.PreferredLanguage != sourceLanguageCode)
                .Select(e => e.PreferredLanguage!)
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "[DEBUG] Employee languages query returned {Count} languages (excluding source '{Source}'): {Languages}",
                employeeLanguageCodes.Count, sourceLanguageCode, string.Join(", ", employeeLanguageCodes));

            if (employeeLanguageCodes.Count == 0)
            {
                _logger.LogInformation(
                    "[DEBUG] No employee languages different from source language '{Source}' found for tenant {TenantId}. Skipping auto-translation.",
                    sourceLanguageCode, tenantId);
                return;
            }

            // Check which translations already exist (e.g., copied from content reuse)
            // and only generate for missing languages to avoid wasteful regeneration
            var existingTranslationCodes = await _toolboxTalksDbContext.ToolboxTalkTranslations
                .IgnoreQueryFilters()
                .Where(t => t.ToolboxTalkId == toolboxTalkId && t.TenantId == tenantId && !t.IsDeleted)
                .Select(t => t.LanguageCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Detect stale translations whose section/question IDs no longer match
            // (e.g., after content reuse followed by content regeneration)
            var staleLanguageCodes = await DetectStaleTranslationsAsync(toolboxTalkId, tenantId, cancellationToken);

            var missingLanguageCodes = employeeLanguageCodes
                .Except(existingTranslationCodes, StringComparer.OrdinalIgnoreCase)
                .Union(staleLanguageCodes, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _logger.LogInformation(
                "[DEBUG] Existing translation languages: {Existing}. Missing: {Missing}. Stale: {Stale}",
                string.Join(", ", existingTranslationCodes),
                string.Join(", ", missingLanguageCodes),
                string.Join(", ", staleLanguageCodes));

            if (missingLanguageCodes.Count == 0)
            {
                _logger.LogInformation(
                    "[DEBUG] All required translations already exist and are up-to-date for ToolboxTalk {ToolboxTalkId}. Skipping auto-translation.",
                    toolboxTalkId);
                return;
            }

            // Convert language codes (e.g. "pl") to language names (e.g. "Polish")
            // as the translation command expects language names
            var languageNames = missingLanguageCodes
                .Select(code => _languageCodeService.GetLanguageName(code))
                .ToList();

            _logger.LogInformation(
                "[DEBUG] Converted to language names: {Names} (from codes: {Codes})",
                string.Join(", ", languageNames), string.Join(", ", employeeLanguageCodes));

            _logger.LogInformation(
                "Auto-generating translations for ToolboxTalk {ToolboxTalkId} in {Count} languages: {Languages}",
                toolboxTalkId, languageNames.Count, string.Join(", ", languageNames));

            await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                new ContentGenerationProgress(
                    "Translating", 92,
                    $"Generating translations for {languageNames.Count} language(s)..."));

            _logger.LogInformation(
                "[DEBUG] Dispatching GenerateContentTranslationsCommand for {Count} languages: {Languages}",
                languageNames.Count, string.Join(", ", languageNames));

            var command = new GenerateContentTranslationsCommand
            {
                ToolboxTalkId = toolboxTalkId,
                TenantId = tenantId,
                TargetLanguages = languageNames
            };

            var translationResult = await _sender.Send(command, cancellationToken);

            _logger.LogInformation(
                "[DEBUG] GenerateContentTranslationsCommand completed. Success: {Success}, " +
                "LanguageResults count: {Count}, Error: {Error}",
                translationResult.Success, translationResult.LanguageResults.Count,
                translationResult.ErrorMessage ?? "none");

            if (translationResult.Success)
            {
                var successCount = translationResult.LanguageResults.Count(r => r.Success);
                var totalCount = translationResult.LanguageResults.Count;

                _logger.LogInformation(
                    "Auto-translation completed for ToolboxTalk {ToolboxTalkId}. " +
                    "Success: {SuccessCount}/{TotalCount} languages",
                    toolboxTalkId, successCount, totalCount);

                foreach (var langResult in translationResult.LanguageResults.Where(r => !r.Success))
                {
                    _logger.LogWarning(
                        "Auto-translation failed for language {Language} ({Code}): {Error}",
                        langResult.Language, langResult.LanguageCode, langResult.ErrorMessage);
                }

                await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                    new ContentGenerationProgress(
                        "Translating", 98,
                        $"Translations complete ({successCount}/{totalCount} languages)."));
            }
            else
            {
                _logger.LogWarning(
                    "Auto-translation failed for ToolboxTalk {ToolboxTalkId}: {Error}",
                    toolboxTalkId, translationResult.ErrorMessage);

                await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                    new ContentGenerationProgress(
                        "Translating", 98,
                        "Translation generation encountered errors. Translations can be generated manually."));
            }
        }
        catch (Exception ex)
        {
            // Translation failure should never fail the content generation job
            _logger.LogError(ex,
                "Auto-translation failed unexpectedly for ToolboxTalk {ToolboxTalkId}. " +
                "Content generation was successful. Translations can be generated manually.",
                toolboxTalkId);

            try
            {
                await SendProgressUpdateAsync(toolboxTalkId, connectionId,
                    new ContentGenerationProgress(
                        "Translating", 98,
                        "Auto-translation skipped due to an error. Translations can be generated manually."));
            }
            catch
            {
                // Swallow - we're already in error handling
            }
        }
    }

    /// <summary>
    /// Detects translations with stale section/question IDs that no longer match live content.
    /// This happens when content reuse copies translations with remapped IDs, but subsequent
    /// content regeneration creates entirely new sections/questions with different IDs.
    /// </summary>
    private async Task<List<string>> DetectStaleTranslationsAsync(
        Guid toolboxTalkId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load current live section and question IDs
            var liveSectionIds = await _toolboxTalksDbContext.ToolboxTalkSections
                .IgnoreQueryFilters()
                .Where(s => s.ToolboxTalkId == toolboxTalkId && !s.IsDeleted)
                .Select(s => s.Id)
                .ToHashSetAsync(cancellationToken);

            var liveQuestionIds = await _toolboxTalksDbContext.ToolboxTalkQuestions
                .IgnoreQueryFilters()
                .Where(q => q.ToolboxTalkId == toolboxTalkId && !q.IsDeleted)
                .Select(q => q.Id)
                .ToHashSetAsync(cancellationToken);

            // If there are no sections or questions, nothing to compare against
            if (liveSectionIds.Count == 0 && liveQuestionIds.Count == 0)
                return [];

            var translations = await _toolboxTalksDbContext.ToolboxTalkTranslations
                .IgnoreQueryFilters()
                .Where(t => t.ToolboxTalkId == toolboxTalkId && t.TenantId == tenantId && !t.IsDeleted)
                .Select(t => new { t.LanguageCode, t.TranslatedSections, t.TranslatedQuestions })
                .ToListAsync(cancellationToken);

            var staleLanguages = new List<string>();

            foreach (var translation in translations)
            {
                var isStale = false;

                // Check section IDs in the JSON
                if (liveSectionIds.Count > 0 && !string.IsNullOrWhiteSpace(translation.TranslatedSections))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(translation.TranslatedSections);
                        foreach (var element in doc.RootElement.EnumerateArray())
                        {
                            if (element.TryGetProperty("SectionId", out var sectionIdProp)
                                && sectionIdProp.TryGetGuid(out var sectionId)
                                && !liveSectionIds.Contains(sectionId))
                            {
                                isStale = true;
                                break;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Malformed JSON counts as stale
                        isStale = true;
                    }
                }

                // Check question IDs in the JSON
                if (!isStale && liveQuestionIds.Count > 0 && !string.IsNullOrWhiteSpace(translation.TranslatedQuestions))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(translation.TranslatedQuestions);
                        foreach (var element in doc.RootElement.EnumerateArray())
                        {
                            if (element.TryGetProperty("QuestionId", out var questionIdProp)
                                && questionIdProp.TryGetGuid(out var questionId)
                                && !liveQuestionIds.Contains(questionId))
                            {
                                isStale = true;
                                break;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        isStale = true;
                    }
                }

                if (isStale)
                {
                    staleLanguages.Add(translation.LanguageCode);
                    _logger.LogInformation(
                        "Translation for language {Language} on ToolboxTalk {ToolboxTalkId} has stale section/question IDs and will be regenerated",
                        translation.LanguageCode, toolboxTalkId);
                }
            }

            return staleLanguages;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to detect stale translations for ToolboxTalk {ToolboxTalkId}. " +
                "Proceeding without stale detection.",
                toolboxTalkId);
            return [];
        }
    }

    /// <summary>
    /// Sends a progress update to the client via SignalR.
    /// </summary>
    private async Task SendProgressUpdateAsync(
        Guid toolboxTalkId,
        string? connectionId,
        ContentGenerationProgress update)
    {
        try
        {
            var payload = new
            {
                toolboxTalkId,
                stage = update.Stage,
                percentComplete = update.PercentComplete,
                message = update.Message
            };

            if (!string.IsNullOrEmpty(connectionId))
            {
                // Send to specific client
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("ContentGenerationProgress", payload);
            }

            // Also send to the group (for multiple listeners)
            await _hubContext.Clients.Group($"content-generation-{toolboxTalkId}")
                .SendAsync("ContentGenerationProgress", payload);

            _logger.LogDebug(
                "Progress update sent for toolbox talk {ToolboxTalkId}: {Stage} - {Percent}%",
                toolboxTalkId, update.Stage, update.PercentComplete);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send progress update for toolbox talk {ToolboxTalkId}",
                toolboxTalkId);
        }
    }

    /// <summary>
    /// Sends a completion notification to the client via SignalR.
    /// </summary>
    private async Task SendCompletionNotificationAsync(
        Guid toolboxTalkId,
        string? connectionId,
        ContentGenerationResult result)
    {
        try
        {
            var payload = new
            {
                toolboxTalkId,
                success = result.Success,
                partialSuccess = result.PartialSuccess,
                sectionsGenerated = result.SectionsGenerated,
                questionsGenerated = result.QuestionsGenerated,
                hasFinalPortionQuestion = result.HasFinalPortionQuestion,
                errors = result.Errors,
                warnings = result.Warnings,
                totalTokensUsed = result.TotalTokensUsed,
                message = GetCompletionMessage(result)
            };

            if (!string.IsNullOrEmpty(connectionId))
            {
                // Send to specific client
                await _hubContext.Clients.Client(connectionId)
                    .SendAsync("ContentGenerationComplete", payload);
            }

            // Also send to the group (for multiple listeners)
            await _hubContext.Clients.Group($"content-generation-{toolboxTalkId}")
                .SendAsync("ContentGenerationComplete", payload);

            _logger.LogDebug(
                "Completion notification sent for toolbox talk {ToolboxTalkId}: Success={Success}",
                toolboxTalkId, result.Success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send completion notification for toolbox talk {ToolboxTalkId}",
                toolboxTalkId);
        }
    }
}
