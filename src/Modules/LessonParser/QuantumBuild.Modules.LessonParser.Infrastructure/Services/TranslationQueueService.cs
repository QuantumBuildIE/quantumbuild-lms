using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.LessonParser.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Services;

/// <summary>
/// Queues content translations for generated talks by enqueuing separate Hangfire
/// background jobs (MissingTranslationsJob) — fire-and-forget so the Lesson Parser
/// job completes immediately. This follows the same pattern used in
/// ToolboxTalksController.SmartGenerate for content-reuse scenarios.
/// Also translates the course title/description into ToolboxTalkCourseTranslation records.
/// </summary>
public class TranslationQueueService : ITranslationQueueService
{
    private readonly ICoreDbContext _coreDbContext;
    private readonly IToolboxTalksDbContext _toolboxTalksDbContext;
    private readonly IContentTranslationService _contentTranslationService;
    private readonly ITranslationJobScheduler _translationJobScheduler;
    private readonly ILanguageCodeService _languageCodeService;
    private readonly ILogger<TranslationQueueService> _logger;

    public TranslationQueueService(
        ICoreDbContext coreDbContext,
        IToolboxTalksDbContext toolboxTalksDbContext,
        IContentTranslationService contentTranslationService,
        ITranslationJobScheduler translationJobScheduler,
        ILanguageCodeService languageCodeService,
        ILogger<TranslationQueueService> logger)
    {
        _coreDbContext = coreDbContext;
        _toolboxTalksDbContext = toolboxTalksDbContext;
        _contentTranslationService = contentTranslationService;
        _translationJobScheduler = translationJobScheduler;
        _languageCodeService = languageCodeService;
        _logger = logger;
    }

    public async Task<TranslationQueueResult> QueueTranslationsForTalksAsync(
        IEnumerable<Guid> talkIds,
        Guid tenantId,
        Guid? courseId = null,
        CancellationToken cancellationToken = default)
    {
        var talkIdList = talkIds.ToList();

        // 1. Get all unique preferred languages for tenant employees
        //    Using IgnoreQueryFilters() because this runs in a background job context
        //    where ICurrentUserService.TenantId may not be set correctly.
        //    Explicit tenant and soft-delete filters are applied instead.
        var employeeLanguageCodes = await _coreDbContext.Employees
            .IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && !e.IsDeleted
                && e.PreferredLanguage != null && e.PreferredLanguage != "en")
            .Select(e => e.PreferredLanguage!)
            .Distinct()
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Employee languages for tenant {TenantId} (excluding source 'en'): {Languages}",
            tenantId, employeeLanguageCodes.Count > 0 ? string.Join(", ", employeeLanguageCodes) : "none");

        // 2. If no languages found, return early
        if (employeeLanguageCodes.Count == 0)
        {
            return new TranslationQueueResult
            {
                HasLanguagesToTranslate = false
            };
        }

        // 3. Convert language codes to names (GenerateContentTranslationsCommand expects names)
        var languageNames = new List<string>();
        foreach (var code in employeeLanguageCodes)
        {
            languageNames.Add(await _languageCodeService.GetLanguageNameAsync(code));
        }

        _logger.LogInformation(
            "Enqueuing translation jobs for {TalkCount} talks in {LanguageCount} languages: {Languages}",
            talkIdList.Count, languageNames.Count, string.Join(", ", languageNames));

        // 4. For each talk, enqueue a separate Hangfire background job (fire-and-forget).
        //    MissingTranslationsJob will detect the required languages and dispatch
        //    GenerateContentTranslationsCommand — the same path used by ContentGenerationJob
        //    and ToolboxTalksController.SmartGenerate.
        var totalJobsQueued = 0;

        foreach (var talkId in talkIdList)
        {
            var jobId = _translationJobScheduler.EnqueueMissingTranslationsJob(talkId, tenantId);
            totalJobsQueued++;

            _logger.LogInformation(
                "Enqueued MissingTranslationsJob {JobId} for talk {TalkId}", jobId, talkId);
        }

        // 5. Translate course title/description if a courseId was provided
        //    (lightweight — just title and description text, not full section/question content)
        if (courseId.HasValue)
        {
            var courseFailures = new List<TranslationFailureEntry>();
            await TranslateCourseAsync(
                courseId.Value, tenantId, employeeLanguageCodes, languageNames, courseFailures, cancellationToken);

            if (courseFailures.Count > 0)
            {
                _logger.LogWarning(
                    "Course translation had {FailureCount} failure(s) for course {CourseId}",
                    courseFailures.Count, courseId.Value);
            }
        }

        _logger.LogInformation(
            "Translation queue complete: {Queued} background jobs enqueued for {Languages} language(s)",
            totalJobsQueued, employeeLanguageCodes.Count);

        return new TranslationQueueResult
        {
            JobsQueued = totalJobsQueued,
            LanguageCodes = employeeLanguageCodes.AsReadOnly(),
            HasLanguagesToTranslate = true
        };
    }

    /// <summary>
    /// Translates course title and description, creating ToolboxTalkCourseTranslation records.
    /// Follows the same pattern as GenerateContentTranslationsCommandHandler for talk translations:
    /// title failure skips the language, description failure is non-fatal.
    /// </summary>
    private async Task TranslateCourseAsync(
        Guid courseId,
        Guid tenantId,
        List<string> languageCodes,
        List<string> languageNames,
        List<TranslationFailureEntry> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            // Load the course
            var course = await _toolboxTalksDbContext.ToolboxTalkCourses
                .IgnoreQueryFilters()
                .Where(c => c.Id == courseId && c.TenantId == tenantId && !c.IsDeleted)
                .Select(c => new { c.Title, c.Description })
                .FirstOrDefaultAsync(cancellationToken);

            if (course == null)
            {
                _logger.LogWarning("Course {CourseId} not found for translation", courseId);
                return;
            }

            // Check for existing translations to avoid violating the unique index (CourseId + LanguageCode)
            var existingCodes = await _toolboxTalksDbContext.ToolboxTalkCourseTranslations
                .IgnoreQueryFilters()
                .Where(t => t.CourseId == courseId && !t.IsDeleted)
                .Select(t => t.LanguageCode)
                .Distinct()
                .ToListAsync(cancellationToken);

            var missingCodes = languageCodes
                .Except(existingCodes, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingCodes.Count == 0)
            {
                _logger.LogInformation(
                    "All course translations already exist for course {CourseId}. Skipping.",
                    courseId);
                return;
            }

            _logger.LogInformation(
                "Translating course {CourseId} title/description into {Count} languages: {Languages}",
                courseId, missingCodes.Count, string.Join(", ", missingCodes));

            foreach (var languageCode in missingCodes)
            {
                // Find the matching language name
                var codeIndex = languageCodes.IndexOf(languageCode);
                var languageName = codeIndex >= 0 ? languageNames[codeIndex] : languageCode;

                try
                {
                    // Translate title (required — skip this language if it fails)
                    var titleResult = await _contentTranslationService.TranslateTextAsync(
                        course.Title, languageName, false, cancellationToken);

                    if (!titleResult.Success)
                    {
                        _logger.LogWarning(
                            "Course title translation failed for {Language}: {Error}",
                            languageName, titleResult.ErrorMessage);

                        failures.Add(new TranslationFailureEntry
                        {
                            TalkId = courseId,
                            Language = languageCode,
                            Reason = $"Course title: {titleResult.ErrorMessage}"
                        });
                        continue;
                    }

                    // Translate description (optional — failure is non-fatal)
                    string? translatedDescription = null;
                    if (!string.IsNullOrWhiteSpace(course.Description))
                    {
                        var descResult = await _contentTranslationService.TranslateTextAsync(
                            course.Description, languageName, false, cancellationToken);

                        translatedDescription = descResult.Success
                            ? descResult.TranslatedContent
                            : null;
                    }

                    // Create the translation entity
                    var translation = new ToolboxTalkCourseTranslation
                    {
                        Id = Guid.NewGuid(),
                        CourseId = courseId,
                        LanguageCode = languageCode,
                        TranslatedTitle = titleResult.TranslatedContent,
                        TranslatedDescription = translatedDescription
                    };

                    _toolboxTalksDbContext.ToolboxTalkCourseTranslations.Add(translation);

                    _logger.LogInformation(
                        "Course translation created for {Language} ({Code})",
                        languageName, languageCode);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Exception translating course {CourseId} to {Language}",
                        courseId, languageName);

                    failures.Add(new TranslationFailureEntry
                    {
                        TalkId = courseId,
                        Language = languageCode,
                        Reason = $"Course: {ex.Message}"
                    });
                }
            }

            // Save all course translations at once
            await _toolboxTalksDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Course {CourseId} translations saved successfully", courseId);
        }
        catch (Exception ex)
        {
            // Course translation failure should not fail the overall process
            _logger.LogError(ex,
                "Failed to translate course {CourseId}. " +
                "Course translations can be generated manually.",
                courseId);
        }
    }

    private record TranslationFailureEntry
    {
        public Guid TalkId { get; init; }
        public string Language { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
    }
}
