using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.LessonParser.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateContentTranslations;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.LessonParser.Infrastructure.Services;

/// <summary>
/// Queues content translations for generated talks by dispatching
/// GenerateContentTranslationsCommand via MediatR — the same mechanism
/// used by ContentGenerationJob.AutoGenerateTranslationsAsync and MissingTranslationsJob.
/// Also translates the course title/description into ToolboxTalkCourseTranslation records.
/// </summary>
public class TranslationQueueService : ITranslationQueueService
{
    private readonly ICoreDbContext _coreDbContext;
    private readonly IToolboxTalksDbContext _toolboxTalksDbContext;
    private readonly IContentTranslationService _contentTranslationService;
    private readonly ISender _sender;
    private readonly ILanguageCodeService _languageCodeService;
    private readonly ILogger<TranslationQueueService> _logger;

    public TranslationQueueService(
        ICoreDbContext coreDbContext,
        IToolboxTalksDbContext toolboxTalksDbContext,
        IContentTranslationService contentTranslationService,
        ISender sender,
        ILanguageCodeService languageCodeService,
        ILogger<TranslationQueueService> logger)
    {
        _coreDbContext = coreDbContext;
        _toolboxTalksDbContext = toolboxTalksDbContext;
        _contentTranslationService = contentTranslationService;
        _sender = sender;
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
            "Dispatching translations for {TalkCount} talks in {LanguageCount} languages: {Languages}",
            talkIdList.Count, languageNames.Count, string.Join(", ", languageNames));

        // 4. For each talk, dispatch the existing GenerateContentTranslationsCommand
        var totalJobsQueued = 0;
        var failures = new List<TranslationFailureEntry>();

        foreach (var talkId in talkIdList)
        {
            try
            {
                var command = new GenerateContentTranslationsCommand
                {
                    ToolboxTalkId = talkId,
                    TenantId = tenantId,
                    TargetLanguages = languageNames
                };

                var result = await _sender.Send(command, cancellationToken);

                if (result.Success)
                {
                    var successCount = result.LanguageResults.Count(r => r.Success);
                    totalJobsQueued += successCount;

                    _logger.LogInformation(
                        "Translations for talk {TalkId}: {SuccessCount}/{TotalCount} languages succeeded",
                        talkId, successCount, result.LanguageResults.Count);

                    // Track per-language failures
                    foreach (var langResult in result.LanguageResults.Where(r => !r.Success))
                    {
                        failures.Add(new TranslationFailureEntry
                        {
                            TalkId = talkId,
                            Language = langResult.LanguageCode,
                            Reason = langResult.ErrorMessage ?? "Unknown error"
                        });
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "Translation command failed for talk {TalkId}: {Error}",
                        talkId, result.ErrorMessage);

                    failures.Add(new TranslationFailureEntry
                    {
                        TalkId = talkId,
                        Language = "*",
                        Reason = result.ErrorMessage ?? "Command failed"
                    });
                }
            }
            catch (Exception ex)
            {
                // Translation failure should not prevent returning results
                _logger.LogError(ex,
                    "Exception dispatching translations for talk {TalkId}. " +
                    "Translations can be generated manually via the Learnings module.",
                    talkId);

                failures.Add(new TranslationFailureEntry
                {
                    TalkId = talkId,
                    Language = "*",
                    Reason = ex.Message
                });
            }
        }

        // 5. Translate course title/description if a courseId was provided
        if (courseId.HasValue)
        {
            await TranslateCourseAsync(
                courseId.Value, tenantId, employeeLanguageCodes, languageNames, failures, cancellationToken);
        }

        var expectedTotal = talkIdList.Count * employeeLanguageCodes.Count;
        var allFailed = totalJobsQueued == 0 && failures.Count > 0;
        var hasPartialFailures = failures.Count > 0 && !allFailed;

        _logger.LogInformation(
            "Translation queue complete: {Queued}/{Expected} succeeded, {Failures} failures",
            totalJobsQueued, expectedTotal, failures.Count);

        return new TranslationQueueResult
        {
            JobsQueued = totalJobsQueued,
            LanguageCodes = employeeLanguageCodes.AsReadOnly(),
            HasLanguagesToTranslate = true,
            FailuresJson = failures.Count > 0
                ? JsonSerializer.Serialize(failures)
                : null,
            HasPartialFailures = hasPartialFailures,
            AllFailed = allFailed
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
