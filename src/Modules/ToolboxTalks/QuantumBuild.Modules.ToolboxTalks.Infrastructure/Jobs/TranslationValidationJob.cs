using System.Diagnostics;
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Prompts;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Hubs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Hangfire background job that orchestrates a full translation validation run.
/// Loads the run, iterates through each section, calls ITranslationValidationService,
/// reports progress via SignalR, and updates the run with aggregate results.
/// If translations are missing (e.g., during the creation wizard flow), generates them first.
/// </summary>
public class TranslationValidationJob
{
    private readonly ITranslationValidationService _validationService;
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IHubContext<TranslationValidationHub> _hubContext;
    private readonly IContentTranslationService _contentTranslationService;
    private readonly ILanguageCodeService _languageCodeService;
    private readonly ISafetyClassificationService _safetyClassificationService;
    private readonly ILogger<TranslationValidationJob> _logger;

    public TranslationValidationJob(
        ITranslationValidationService validationService,
        IToolboxTalksDbContext dbContext,
        IHubContext<TranslationValidationHub> hubContext,
        IContentTranslationService contentTranslationService,
        ILanguageCodeService languageCodeService,
        ISafetyClassificationService safetyClassificationService,
        ILogger<TranslationValidationJob> logger)
    {
        _validationService = validationService;
        _dbContext = dbContext;
        _hubContext = hubContext;
        _contentTranslationService = contentTranslationService;
        _languageCodeService = languageCodeService;
        _safetyClassificationService = safetyClassificationService;
        _logger = logger;
    }

    /// <summary>
    /// Executes the translation validation job for a given run.
    /// </summary>
    /// <param name="validationRunId">The validation run to process</param>
    /// <param name="tenantId">Tenant ID (passed explicitly since Hangfire runs outside HTTP context)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [AutomaticRetry(Attempts = 1)]
    [Queue("content-generation")]
    public async Task ExecuteAsync(
        Guid validationRunId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "========== TRANSLATION VALIDATION JOB STARTED ==========\n" +
            "ValidationRunId: {RunId}\n" +
            "TenantId: {TenantId}",
            validationRunId, tenantId);

        try
        {
            // Load the validation run
            var run = await _dbContext.TranslationValidationRuns
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == validationRunId && r.TenantId == tenantId && !r.IsDeleted,
                    cancellationToken);

            if (run == null)
            {
                _logger.LogError(
                    "Validation run {RunId} not found for tenant {TenantId}",
                    validationRunId, tenantId);
                return;
            }

            // Mark as running
            run.Status = ValidationRunStatus.Running;
            run.StartedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await SendProgressAsync(validationRunId, "Starting", 0, "Preparing validation run...");

            // Load sections to validate
            var sections = await LoadSectionsAsync(run, tenantId, cancellationToken);

            if (sections.Count == 0)
            {
                _logger.LogWarning(
                    "No sections to validate for run {RunId}. Marking as completed.",
                    validationRunId);

                run.Status = ValidationRunStatus.Completed;
                run.CompletedAt = DateTime.UtcNow;
                run.TotalSections = 0;
                await _dbContext.SaveChangesAsync(cancellationToken);

                await SendCompletionAsync(validationRunId, true, "No sections to validate");
                return;
            }

            _logger.LogInformation(
                "Loaded {Count} section(s) to validate for run {RunId}",
                sections.Count, validationRunId);

            run.TotalSections = sections.Count;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Validate each section (upsert logic in ValidateSectionAsync handles retry scenarios)
            var results = new List<Domain.Entities.TranslationValidationResult>();

            for (int i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                var percentComplete = (int)((double)(i + 1) / sections.Count * 90) + 5; // 5-95%

                await SendProgressAsync(validationRunId,
                    "Validating",
                    percentComplete,
                    $"Validating section {i + 1} of {sections.Count}: {section.Title}...");

                try
                {
                    var result = await _validationService.ValidateSectionAsync(
                        validationRunId,
                        section.Index,
                        section.Title,
                        section.OriginalText,
                        section.TranslatedText,
                        run.SourceLanguage,
                        run.LanguageCode,
                        run.SectorKey,
                        run.PassThreshold,
                        cancellationToken,
                        tenantId: tenantId,
                        toolboxTalkId: run.ToolboxTalkId);

                    results.Add(result);

                    await SendSectionCompletedAsync(validationRunId, result, percentComplete);

                    _logger.LogInformation(
                        "Section {Index}/{Total} '{Title}' validated: {Outcome} (Score: {Score})",
                        i + 1, sections.Count, section.Title,
                        result.Outcome, result.FinalScore);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to validate section {Index} '{Title}' in run {RunId}. " +
                        "Exception: {ExType}: {ExMessage}",
                        i, section.Title, validationRunId,
                        ex.GetType().FullName, ex.Message);

                    // Upsert a failed result so the run can still complete
                    var failedResult = await _dbContext.TranslationValidationResults
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(r => r.ValidationRunId == validationRunId
                            && r.SectionIndex == section.Index, cancellationToken);

                    if (failedResult == null)
                    {
                        failedResult = new Domain.Entities.TranslationValidationResult
                        {
                            ValidationRunId = validationRunId,
                            SectionIndex = section.Index
                        };
                        _dbContext.TranslationValidationResults.Add(failedResult);
                    }

                    failedResult.SectionTitle = section.Title;
                    failedResult.OriginalText = section.OriginalText;
                    failedResult.TranslatedText = section.TranslatedText;
                    failedResult.Outcome = ValidationOutcome.Fail;
                    failedResult.FinalScore = 0;
                    failedResult.RoundsUsed = 0;
                    failedResult.EffectiveThreshold = run.PassThreshold;

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    results.Add(failedResult);
                }
            }

            // Calculate aggregate results
            await SendProgressAsync(validationRunId, "Finalizing", 95, "Calculating aggregate results...");

            run.PassedSections = results.Count(r => r.Outcome == ValidationOutcome.Pass);
            run.ReviewSections = results.Count(r => r.Outcome == ValidationOutcome.Review);
            run.FailedSections = results.Count(r => r.Outcome == ValidationOutcome.Fail);
            run.OverallScore = results.Count > 0
                ? (int)Math.Round(results.Average(r => (double)r.FinalScore))
                : 0;
            run.OverallOutcome = DetermineOverallOutcome(run);
            run.SafetyVerdict = DetermineSafetyVerdict(results);
            run.Status = ValidationRunStatus.Completed;
            run.CompletedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "========== TRANSLATION VALIDATION JOB COMPLETED ==========\n" +
                "ValidationRunId: {RunId}\n" +
                "Duration: {Duration}ms ({DurationSeconds:F1}s)\n" +
                "TotalSections: {Total}\n" +
                "Passed: {Passed}, Review: {Review}, Failed: {Failed}\n" +
                "OverallScore: {Score}, OverallOutcome: {Outcome}\n" +
                "SafetyVerdict: {Safety}",
                validationRunId,
                stopwatch.ElapsedMilliseconds,
                stopwatch.ElapsedMilliseconds / 1000.0,
                run.TotalSections,
                run.PassedSections, run.ReviewSections, run.FailedSections,
                run.OverallScore, run.OverallOutcome,
                run.SafetyVerdict);

            await SendCompletionAsync(validationRunId, true,
                $"Validation complete: {run.PassedSections}/{run.TotalSections} passed, " +
                $"score {run.OverallScore}%");

            // If this run belongs to a creation session, check if all runs are done
            await TryUpdateSessionStatusAsync(validationRunId, tenantId);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "========== TRANSLATION VALIDATION JOB CANCELLED ==========\n" +
                "ValidationRunId: {RunId}\n" +
                "Duration before cancellation: {Duration}ms",
                validationRunId, stopwatch.ElapsedMilliseconds);

            await UpdateRunStatusAsync(validationRunId, tenantId, ValidationRunStatus.Cancelled);
            await SendCompletionAsync(validationRunId, false, "Validation was cancelled");

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "========== TRANSLATION VALIDATION JOB EXCEPTION ==========\n" +
                "ValidationRunId: {RunId}\n" +
                "TenantId: {TenantId}\n" +
                "Duration before error: {Duration}ms\n" +
                "Exception: {ExType}: {Message}\n" +
                "StackTrace: {Stack}",
                validationRunId,
                tenantId,
                stopwatch.ElapsedMilliseconds,
                ex.GetType().FullName,
                ex.Message,
                ex.StackTrace);

            await UpdateRunStatusAsync(validationRunId, tenantId, ValidationRunStatus.Failed);

            // Surface a meaningful error to the frontend instead of a generic fallback
            var clientMessage = ex switch
            {
                InvalidOperationException => $"Validation configuration error: {ex.Message}",
                HttpRequestException => $"External translation service unavailable: {ex.Message}",
                TaskCanceledException => "Validation timed out — an external provider did not respond in time",
                JsonException => $"Failed to parse translation data: {ex.Message}",
                Microsoft.EntityFrameworkCore.DbUpdateException => $"Database error while saving results: {ex.Message}",
                _ => $"Validation failed: {ex.Message}"
            };

            await SendCompletionAsync(validationRunId, false, clientMessage);

            throw;
        }
    }

    /// <summary>
    /// Loads the original sections and their translated counterparts for validation.
    /// </summary>
    private async Task<List<SectionPair>> LoadSectionsAsync(
        Domain.Entities.TranslationValidationRun run,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (run.ToolboxTalkId == null)
        {
            _logger.LogWarning("Validation run {RunId} has no ToolboxTalkId — course validation not yet supported",
                run.Id);
            return [];
        }

        // Load original sections
        var originalSections = await _dbContext.ToolboxTalkSections
            .IgnoreQueryFilters()
            .Where(s => s.ToolboxTalkId == run.ToolboxTalkId && !s.IsDeleted)
            .OrderBy(s => s.SectionNumber)
            .Select(s => new OriginalSectionInfo(s.Id, s.SectionNumber, s.Title, s.Content))
            .ToListAsync(cancellationToken);

        if (originalSections.Count == 0)
            return [];

        // Load the translation for the target language
        var translation = await _dbContext.ToolboxTalkTranslations
            .IgnoreQueryFilters()
            .Where(t => t.ToolboxTalkId == run.ToolboxTalkId
                && t.TenantId == tenantId
                && t.LanguageCode == run.LanguageCode
                && !t.IsDeleted)
            .Select(t => t.TranslatedSections)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(translation))
        {
            _logger.LogInformation(
                "No translation found for ToolboxTalk {TalkId}, Language {Lang} — generating now",
                run.ToolboxTalkId, run.LanguageCode);

            // Generate translations inline (creation wizard flow — translations don't exist yet)
            // Pass sectorKey from the validation run for tiered prompt quality
            translation = await GenerateTranslationForSectionsAsync(
                run.ToolboxTalkId.Value, tenantId, run.LanguageCode, originalSections,
                run.SectorKey, run.SourceLanguage, cancellationToken);

            if (string.IsNullOrWhiteSpace(translation))
            {
                _logger.LogWarning(
                    "Translation generation failed for ToolboxTalk {TalkId}, Language {Lang}",
                    run.ToolboxTalkId, run.LanguageCode);
                return [];
            }
        }

        // Parse translated sections JSON: [{SectionId, Title, Content}]
        var translatedSections = new Dictionary<Guid, TranslatedSectionJson>();
        try
        {
            using var doc = JsonDocument.Parse(translation);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("SectionId", out var sectionIdProp)
                    && sectionIdProp.TryGetGuid(out var sectionId))
                {
                    var title = element.TryGetProperty("Title", out var titleProp)
                        ? titleProp.GetString() ?? string.Empty
                        : string.Empty;
                    var content = element.TryGetProperty("Content", out var contentProp)
                        ? contentProp.GetString() ?? string.Empty
                        : string.Empty;

                    translatedSections[sectionId] = new TranslatedSectionJson(title, content);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to parse TranslatedSections JSON for ToolboxTalk {TalkId}, Language {Lang}",
                run.ToolboxTalkId, run.LanguageCode);
            return [];
        }

        // Pair original and translated sections
        var pairs = new List<SectionPair>();
        for (int i = 0; i < originalSections.Count; i++)
        {
            var orig = originalSections[i];
            if (translatedSections.TryGetValue(orig.Id, out var translated))
            {
                // Strip HTML tags for text comparison
                var originalText = StripHtml(orig.Content);
                var translatedText = StripHtml(translated.Content);

                if (!string.IsNullOrWhiteSpace(originalText) && !string.IsNullOrWhiteSpace(translatedText))
                {
                    pairs.Add(new SectionPair(i, orig.Title, originalText, translatedText));
                }
            }
            else
            {
                _logger.LogDebug(
                    "No translated content found for section {SectionId} '{Title}' — skipping",
                    orig.Id, orig.Title);
            }
        }

        return pairs;
    }

    /// <summary>
    /// Determines the overall outcome for the run based on section results.
    /// </summary>
    private static ValidationOutcome DetermineOverallOutcome(Domain.Entities.TranslationValidationRun run)
    {
        if (run.FailedSections > 0)
            return ValidationOutcome.Fail;

        if (run.ReviewSections > 0)
            return ValidationOutcome.Review;

        return ValidationOutcome.Pass;
    }

    /// <summary>
    /// Determines the safety verdict based on safety-critical section results.
    /// </summary>
    private static ValidationOutcome? DetermineSafetyVerdict(
        List<Domain.Entities.TranslationValidationResult> results)
    {
        var safetyCriticalResults = results.Where(r => r.IsSafetyCritical).ToList();
        if (safetyCriticalResults.Count == 0)
            return null; // No safety-critical sections

        if (safetyCriticalResults.Any(r => r.Outcome == ValidationOutcome.Fail))
            return ValidationOutcome.Fail;

        if (safetyCriticalResults.Any(r => r.Outcome == ValidationOutcome.Review))
            return ValidationOutcome.Review;

        return ValidationOutcome.Pass;
    }

    /// <summary>
    /// Updates the run status in the database (for error/cancellation paths).
    /// </summary>
    private async Task UpdateRunStatusAsync(Guid validationRunId, Guid tenantId, ValidationRunStatus status)
    {
        try
        {
            var run = await _dbContext.TranslationValidationRuns
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.Id == validationRunId && r.TenantId == tenantId && !r.IsDeleted);

            if (run != null)
            {
                run.Status = status;
                run.CompletedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to update run {RunId} status to {Status}",
                validationRunId, status);
        }
    }

    /// <summary>
    /// Sends a progress update via SignalR.
    /// </summary>
    private async Task SendProgressAsync(Guid validationRunId, string stage, int percentComplete, string message)
    {
        try
        {
            var payload = new
            {
                validationRunId,
                stage,
                percentComplete,
                message
            };

            await _hubContext.Clients.Group($"validation-{validationRunId}")
                .SendAsync("ValidationProgress", payload);

            _logger.LogDebug(
                "Progress update sent for run {RunId}: {Stage} - {Percent}%",
                validationRunId, stage, percentComplete);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send progress update for run {RunId}",
                validationRunId);
        }
    }

    /// <summary>
    /// Sends a section-completed notification via SignalR with the section result details.
    /// </summary>
    private async Task SendSectionCompletedAsync(
        Guid validationRunId,
        Domain.Entities.TranslationValidationResult result,
        int percentComplete)
    {
        try
        {
            var payload = new
            {
                validationRunId,
                sectionIndex = result.SectionIndex,
                outcome = result.Outcome.ToString(),
                finalScore = result.FinalScore,
                isSafetyCritical = result.IsSafetyCritical,
                glossaryMismatches = result.GlossaryMismatches,
                percentComplete
            };

            await _hubContext.Clients.Group($"validation-{validationRunId}")
                .SendAsync("SectionCompleted", payload);

            _logger.LogDebug(
                "SectionCompleted sent for run {RunId}: Section {Index}, Outcome={Outcome}, Score={Score}",
                validationRunId, result.SectionIndex, result.Outcome, result.FinalScore);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send SectionCompleted for run {RunId}, section {Index}",
                validationRunId, result.SectionIndex);
        }
    }

    /// <summary>
    /// Sends a completion notification via SignalR.
    /// </summary>
    private async Task SendCompletionAsync(Guid validationRunId, bool success, string message)
    {
        try
        {
            var payload = new
            {
                validationRunId,
                success,
                message
            };

            await _hubContext.Clients.Group($"validation-{validationRunId}")
                .SendAsync("ValidationComplete", payload);

            _logger.LogDebug(
                "Completion notification sent for run {RunId}: Success={Success}",
                validationRunId, success);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send completion notification for run {RunId}",
                validationRunId);
        }
    }

    /// <summary>
    /// Strips HTML tags from content for text-level comparison.
    /// </summary>
    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Remove HTML tags
        var text = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", " ");
        // Decode common HTML entities
        text = text.Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&quot;", "\"")
                   .Replace("&#39;", "'")
                   .Replace("&nbsp;", " ");
        // Collapse whitespace
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    /// <summary>
    /// Generates translations for sections when they don't exist yet (creation wizard flow).
    /// Creates a ToolboxTalkTranslation record and returns the TranslatedSections JSON.
    /// Uses tiered prompts when sectorKey is available for compliance-quality translations.
    /// </summary>
    private async Task<string?> GenerateTranslationForSectionsAsync(
        Guid talkId,
        Guid tenantId,
        string languageCode,
        List<OriginalSectionInfo> originalSections,
        string? sectorKey,
        string? sourceLanguage,
        CancellationToken cancellationToken)
    {
        try
        {
            var languageName = await _languageCodeService.GetLanguageNameAsync(languageCode);
            var source = sourceLanguage ?? "English";

            _logger.LogInformation(
                "Generating translations for {Count} sections to {Language} ({Code}), SectorKey: {SectorKey}",
                originalSections.Count, languageName, languageCode, sectorKey ?? "(none)");

            // Load glossary terms for this sector + language (once, before section loop)
            List<GlossaryTermInstruction> glossaryInstructions = new();
            if (!string.IsNullOrEmpty(sectorKey))
            {
                glossaryInstructions = await LoadGlossaryTermsAsync(
                    sectorKey, tenantId, languageCode, cancellationToken);

                _logger.LogInformation(
                    "Loaded {Count} glossary term instructions for sector {Sector}, language {Lang}",
                    glossaryInstructions.Count, sectorKey, languageCode);
            }

            var translatedSections = new List<object>();

            foreach (var section in originalSections)
            {
                var sectionId = section.Id;
                var title = section.Title;
                var content = section.Content;

                // Classify safety criticality before translation
                var isSafetyCritical = false;
                if (!string.IsNullOrEmpty(sectorKey))
                {
                    var classification = await _safetyClassificationService.ClassifyAsync(
                        content, sectorKey, languageCode, cancellationToken);
                    isSafetyCritical = classification.IsSafetyCritical;
                }

                // Section title: sector-aware but not safety-critical, no glossary
                var titleResult = await _contentTranslationService.TranslateTextAsync(
                    title, languageName, false, cancellationToken,
                    sourceLanguage: source,
                    sectorKey: sectorKey,
                    isSafetyCritical: false,
                    glossaryTerms: null,
                    tenantId: tenantId,
                    isSystemCall: true,
                    toolboxTalkId: talkId);

                // Section content: full tiered prompt with safety + glossary
                var contentResult = await _contentTranslationService.TranslateTextAsync(
                    content, languageName, true, cancellationToken,
                    sourceLanguage: source,
                    sectorKey: sectorKey,
                    isSafetyCritical: isSafetyCritical,
                    glossaryTerms: glossaryInstructions,
                    tenantId: tenantId,
                    isSystemCall: true,
                    toolboxTalkId: talkId);

                if (titleResult.Success && contentResult.Success)
                {
                    translatedSections.Add(new
                    {
                        SectionId = sectionId,
                        Title = titleResult.TranslatedContent,
                        Content = contentResult.TranslatedContent
                    });
                }
                else
                {
                    _logger.LogWarning(
                        "Translation failed for section {SectionId} to {Language}. Title: {TitleOk}, Content: {ContentOk}",
                        sectionId, languageName, titleResult.Success, contentResult.Success);
                }
            }

            if (translatedSections.Count == 0)
                return null;

            var translatedSectionsJson = JsonSerializer.Serialize(translatedSections);

            // Load the talk to translate title, description, and quiz questions
            var talk = await _dbContext.ToolboxTalks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == talkId && !t.IsDeleted, cancellationToken);

            var translatedTitle = talk?.Title ?? "Untitled";
            string? translatedDescription = null;
            if (talk != null)
            {
                var titleTranslation = await _contentTranslationService.TranslateTextAsync(
                    talk.Title, languageName, false, cancellationToken, sectorKey: sectorKey,
                    tenantId: tenantId, isSystemCall: true, toolboxTalkId: talkId);
                if (titleTranslation.Success)
                    translatedTitle = titleTranslation.TranslatedContent;

                // Translate description if present
                if (!string.IsNullOrWhiteSpace(talk.Description))
                {
                    var descResult = await _contentTranslationService.TranslateTextAsync(
                        talk.Description, languageName, false, cancellationToken, sectorKey: sectorKey,
                        tenantId: tenantId, isSystemCall: true, toolboxTalkId: talkId);
                    if (descResult.Success)
                        translatedDescription = descResult.TranslatedContent;
                }
            }

            // Translate quiz questions if the talk has any
            string? translatedQuestionsJson = null;
            var questions = await _dbContext.ToolboxTalkQuestions
                .IgnoreQueryFilters()
                .Where(q => q.ToolboxTalkId == talkId)
                .OrderBy(q => q.QuestionNumber)
                .ToListAsync(cancellationToken);

            if (questions.Count > 0)
            {
                var translatedQuestions = new List<object>();
                foreach (var q in questions)
                {
                    // Translate question text — sector-aware for compliance terminology
                    var qTextResult = await _contentTranslationService.TranslateTextAsync(
                        q.QuestionText, languageName, false, cancellationToken, sectorKey: sectorKey,
                        tenantId: tenantId, isSystemCall: true, toolboxTalkId: talkId);
                    var translatedQuestionText = qTextResult.Success
                        ? qTextResult.TranslatedContent
                        : q.QuestionText;

                    // Translate options if present
                    List<string>? translatedOptions = null;
                    if (!string.IsNullOrWhiteSpace(q.Options))
                    {
                        try
                        {
                            var options = JsonSerializer.Deserialize<List<string>>(q.Options) ?? new();
                            translatedOptions = new List<string>();
                            foreach (var option in options)
                            {
                                var optResult = await _contentTranslationService.TranslateTextAsync(
                                    option, languageName, false, cancellationToken, sectorKey: sectorKey,
                                    tenantId: tenantId, isSystemCall: true, toolboxTalkId: talkId);
                                translatedOptions.Add(optResult.Success ? optResult.TranslatedContent : option);
                            }
                        }
                        catch (JsonException)
                        {
                            _logger.LogWarning(
                                "Failed to parse options JSON for question {QuestionId}", q.Id);
                        }
                    }

                    translatedQuestions.Add(new
                    {
                        QuestionId = q.Id,
                        QuestionNumber = q.QuestionNumber,
                        QuestionText = translatedQuestionText,
                        QuestionType = q.QuestionType.ToString(),
                        Options = translatedOptions,
                        CorrectOptionIndex = q.CorrectOptionIndex,
                        Points = q.Points
                    });
                }

                translatedQuestionsJson = JsonSerializer.Serialize(translatedQuestions);
                _logger.LogInformation(
                    "Translated {Count} quiz questions to {Language}",
                    translatedQuestions.Count, languageName);
            }

            // Persist the translation record
            var translation = new Domain.Entities.ToolboxTalkTranslation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ToolboxTalkId = talkId,
                LanguageCode = languageCode,
                TranslatedTitle = translatedTitle,
                TranslatedDescription = translatedDescription,
                TranslatedSections = translatedSectionsJson,
                TranslatedQuestions = translatedQuestionsJson,
                TranslatedAt = DateTime.UtcNow,
                TranslationProvider = "Claude",
                EmailSubject = translatedTitle,
                EmailBody = translatedTitle
            };

            _dbContext.ToolboxTalkTranslations.Add(translation);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Generated translation for {Count}/{Total} sections to {Language}",
                translatedSections.Count, originalSections.Count, languageName);

            return translatedSectionsJson;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to generate translations for ToolboxTalk {TalkId}, Language {Lang}",
                talkId, languageCode);
            return null;
        }
    }

    /// <summary>
    /// Loads glossary terms for a sector + language, preferring tenant overrides over system defaults.
    /// Returns GlossaryTermInstruction list for terms that have a translation for the target language.
    /// </summary>
    private async Task<List<GlossaryTermInstruction>> LoadGlossaryTermsAsync(
        string sectorKey,
        Guid tenantId,
        string languageCode,
        CancellationToken cancellationToken)
    {
        // Load all matching glossary terms — tenant-specific and system defaults
        var terms = await _dbContext.SafetyGlossaryTerms
            .IgnoreQueryFilters()
            .Include(t => t.Glossary)
            .Where(t => !t.IsDeleted
                && !t.Glossary.IsDeleted
                && t.Glossary.SectorKey == sectorKey
                && (t.Glossary.TenantId == tenantId || t.Glossary.TenantId == null))
            .ToListAsync(cancellationToken);

        // Prefer tenant override over system default for each English term
        var grouped = terms.GroupBy(t => t.EnglishTerm.ToLowerInvariant());
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var instructions = new List<GlossaryTermInstruction>();

        foreach (var group in grouped)
        {
            var term = group.FirstOrDefault(t => t.Glossary.TenantId == tenantId)
                       ?? group.First();

            if (string.IsNullOrWhiteSpace(term.Translations))
                continue;

            try
            {
                var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(
                    term.Translations, options);

                if (translations != null
                    && translations.TryGetValue(languageCode, out var translated)
                    && !string.IsNullOrWhiteSpace(translated))
                {
                    instructions.Add(new GlossaryTermInstruction(term.EnglishTerm, translated));
                }
            }
            catch (JsonException)
            {
                _logger.LogDebug(
                    "Failed to parse Translations JSON for glossary term {TermId} '{Term}'",
                    term.Id, term.EnglishTerm);
            }
        }

        return instructions;
    }

    /// <summary>
    /// Checks if this validation run belongs to a content creation session,
    /// and if all runs for the session are complete, transitions the session to Validated.
    /// </summary>
    private async Task TryUpdateSessionStatusAsync(Guid validationRunId, Guid tenantId)
    {
        try
        {
            // Find any session that references this run ID in its ValidationRunIds JSON
            var sessions = await _dbContext.ContentCreationSessions
                .IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId
                    && s.Status == Domain.Enums.ContentCreationSessionStatus.TranslatingValidating
                    && s.ValidationRunIds != null
                    && !s.IsDeleted)
                .ToListAsync();

            var runIdString = validationRunId.ToString();

            foreach (var session in sessions)
            {
                if (session.ValidationRunIds == null || !session.ValidationRunIds.Contains(runIdString))
                    continue;

                // Parse the run IDs from JSON
                List<Guid> runIds;
                try
                {
                    runIds = JsonSerializer.Deserialize<List<Guid>>(session.ValidationRunIds) ?? new();
                }
                catch
                {
                    continue;
                }

                if (!runIds.Contains(validationRunId))
                    continue;

                // Check if ALL runs for this session are completed
                var allRunStatuses = await _dbContext.TranslationValidationRuns
                    .IgnoreQueryFilters()
                    .Where(r => runIds.Contains(r.Id) && !r.IsDeleted)
                    .Select(r => r.Status)
                    .ToListAsync();

                var allComplete = allRunStatuses.Count > 0 &&
                    allRunStatuses.All(s => s == ValidationRunStatus.Completed || s == ValidationRunStatus.Failed);

                if (allComplete)
                {
                    session.Status = Domain.Enums.ContentCreationSessionStatus.Validated;
                    await _dbContext.SaveChangesAsync();

                    _logger.LogInformation(
                        "Session {SessionId} transitioned to Validated — all {Count} runs complete",
                        session.Id, runIds.Count);
                }

                break; // A run belongs to at most one session
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to check/update session status after run {RunId} completed",
                validationRunId);
        }
    }

    /// <summary>
    /// Represents a paired original/translated section for validation.
    /// </summary>
    private sealed record SectionPair(int Index, string Title, string OriginalText, string TranslatedText);

    /// <summary>
    /// Parsed translated section from the JSON array.
    /// </summary>
    private sealed record TranslatedSectionJson(string Title, string Content);

    /// <summary>
    /// Projection of an original section loaded from the database.
    /// </summary>
    private sealed record OriginalSectionInfo(Guid Id, int SectionNumber, string Title, string Content);
}
