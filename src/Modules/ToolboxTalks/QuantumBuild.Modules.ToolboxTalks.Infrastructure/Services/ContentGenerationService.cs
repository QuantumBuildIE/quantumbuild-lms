using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Orchestrates the full content generation flow for toolbox talks:
/// extract content, generate sections, generate quiz questions, and save to database.
/// </summary>
public class ContentGenerationService : IContentGenerationService
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IContentExtractionService _extractionService;
    private readonly IAiSectionGenerationService _sectionService;
    private readonly IAiQuizGenerationService _quizService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ContentGenerationService> _logger;

    public ContentGenerationService(
        IToolboxTalksDbContext dbContext,
        IContentExtractionService extractionService,
        IAiSectionGenerationService sectionService,
        IAiQuizGenerationService quizService,
        ICurrentUserService currentUser,
        ILogger<ContentGenerationService> logger)
    {
        _dbContext = dbContext;
        _extractionService = extractionService;
        _sectionService = sectionService;
        _quizService = quizService;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ContentGenerationResult> GenerateContentAsync(
        Guid toolboxTalkId,
        ContentGenerationOptions options,
        Guid tenantId,
        IProgress<ContentGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var totalTokens = 0;
        var sectionsGenerated = 0;
        var questionsGenerated = 0;
        var hasFinalPortionQuestion = false;
        var extractionWasPartial = false;

        _logger.LogInformation(
            "[ContentGenerationService] Starting content generation for toolbox talk {Id}. " +
            "TenantId: {TenantId}, Options: IncludeVideo={IncludeVideo}, IncludePdf={IncludePdf}",
            toolboxTalkId, tenantId, options.IncludeVideo, options.IncludePdf);

        try
        {
            // Get the toolbox talk
            _logger.LogDebug("[Step: Fetch] Loading toolbox talk {Id} from database...", toolboxTalkId);

            var toolboxTalk = await _dbContext.ToolboxTalks
                .Include(t => t.Sections)
                .Include(t => t.Questions)
                .FirstOrDefaultAsync(t => t.Id == toolboxTalkId && t.TenantId == tenantId, cancellationToken);

            if (toolboxTalk == null)
            {
                _logger.LogWarning(
                    "[Step: Fetch] FAILED - Toolbox talk {Id} not found for tenant {TenantId}",
                    toolboxTalkId, tenantId);
                return new ContentGenerationResult(
                    Success: false,
                    PartialSuccess: false,
                    SectionsGenerated: 0,
                    QuestionsGenerated: 0,
                    HasFinalPortionQuestion: false,
                    Errors: new List<string> { "Toolbox Talk not found" },
                    Warnings: new List<string>(),
                    TotalTokensUsed: 0);
            }

            _logger.LogInformation(
                "[Step: Fetch] SUCCESS - Loaded toolbox talk: Title='{Title}', VideoUrl='{VideoUrl}', PdfUrl='{PdfUrl}', " +
                "ExistingSections={ExistingSections}, ExistingQuestions={ExistingQuestions}",
                toolboxTalk.Title,
                string.IsNullOrEmpty(toolboxTalk.VideoUrl) ? "(none)" : toolboxTalk.VideoUrl,
                string.IsNullOrEmpty(toolboxTalk.PdfUrl) ? "(none)" : toolboxTalk.PdfUrl,
                toolboxTalk.Sections.Count(s => !s.IsDeleted),
                toolboxTalk.Questions.Count(q => !q.IsDeleted));

            // Update status to Processing
            toolboxTalk.Status = ToolboxTalkStatus.Processing;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("[Step: Status] Updated toolbox talk status to Processing");

            // Stage 1: Extract content (10-30%)
            _logger.LogInformation(
                "[Step: Extract] Starting content extraction for toolbox talk {Id}. IncludeVideo={IncludeVideo}, IncludePdf={IncludePdf}",
                toolboxTalkId, options.IncludeVideo, options.IncludePdf);

            progress?.Report(new ContentGenerationProgress(
                "Extracting", 10, "Extracting content from video and PDF..."));

            var extractionResult = await _extractionService.ExtractContentAsync(
                toolboxTalkId, options.IncludeVideo, options.IncludePdf, tenantId, cancellationToken);

            if (!extractionResult.Success)
            {
                _logger.LogError(
                    "[Step: Extract] FAILED - Content extraction failed for toolbox talk {Id}. Errors: {Errors}",
                    toolboxTalkId, string.Join("; ", extractionResult.Errors));

                errors.AddRange(extractionResult.Errors);

                // Reset status to Draft on failure
                toolboxTalk.Status = ToolboxTalkStatus.Draft;
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new ContentGenerationResult(
                    Success: false,
                    PartialSuccess: false,
                    SectionsGenerated: 0,
                    QuestionsGenerated: 0,
                    HasFinalPortionQuestion: false,
                    Errors: errors,
                    Warnings: warnings,
                    TotalTokensUsed: 0);
            }

            // Track if extraction was partial (some sources failed but we still have content)
            extractionWasPartial = extractionResult.PartialSuccess;

            _logger.LogInformation(
                "[Step: Extract] SUCCESS - Content extraction completed. " +
                "HasVideoContent={HasVideo}, HasPdfContent={HasPdf}, " +
                "CombinedContentLength={ContentLength} chars, Warnings={WarningCount}",
                extractionResult.VideoContent != null,
                extractionResult.PdfContent != null,
                extractionResult.CombinedContent?.Length ?? 0,
                extractionResult.Warnings.Count);

            if (extractionResult.VideoContent != null)
            {
                _logger.LogDebug(
                    "[Step: Extract] Video details: FullTranscriptLength={TranscriptLength}, " +
                    "FinalPortionLength={FinalLength}, Duration={Duration}, Segments={Segments}",
                    extractionResult.VideoContent.FullTranscript.Length,
                    extractionResult.VideoContent.FinalPortionTranscript?.Length ?? 0,
                    extractionResult.VideoContent.Duration,
                    extractionResult.VideoContent.SegmentCount);
            }

            if (extractionResult.PdfContent != null)
            {
                _logger.LogDebug(
                    "[Step: Extract] PDF details: TextLength={TextLength}, PageCount={Pages}",
                    extractionResult.PdfContent.FullText.Length,
                    extractionResult.PdfContent.PageCount);
            }

            warnings.AddRange(extractionResult.Warnings);
            progress?.Report(new ContentGenerationProgress(
                "Extracting", 30, "Content extraction complete."));

            // Stage 2: Generate sections (30-60%)
            _logger.LogInformation(
                "[Step: Sections] Starting AI section generation for toolbox talk {Id}. MinSections={MinSections}",
                toolboxTalkId, options.MinimumSections);

            progress?.Report(new ContentGenerationProgress(
                "GeneratingSections", 35, "AI is generating sections..."));

            var sectionResult = await _sectionService.GenerateSectionsAsync(
                toolboxTalkId,
                extractionResult.CombinedContent!,
                extractionResult.VideoContent != null,
                extractionResult.PdfContent != null,
                options.MinimumSections,
                cancellationToken);

            totalTokens += sectionResult.TokensUsed;

            if (!sectionResult.Success)
            {
                _logger.LogError(
                    "[Step: Sections] FAILED - Section generation failed for toolbox talk {Id}. " +
                    "Error: {Error}, TokensUsed: {Tokens}",
                    toolboxTalkId, sectionResult.ErrorMessage, sectionResult.TokensUsed);
                errors.Add($"Section generation failed: {sectionResult.ErrorMessage}");
            }
            else
            {
                sectionsGenerated = sectionResult.Sections.Count;
                _logger.LogInformation(
                    "[Step: Sections] SUCCESS - Generated {Count} sections for toolbox talk {Id}. TokensUsed: {Tokens}",
                    sectionsGenerated, toolboxTalkId, sectionResult.TokensUsed);

                foreach (var section in sectionResult.Sections)
                {
                    _logger.LogDebug(
                        "[Step: Sections] Section {SortOrder}: '{Title}' (Source: {Source}, ContentLength: {Length})",
                        section.SortOrder, section.Title, section.Source, section.Content.Length);
                }
            }

            progress?.Report(new ContentGenerationProgress(
                "GeneratingSections", 60, $"Generated {sectionsGenerated} sections."));

            // Stage 3: Generate quiz (60-85%)
            _logger.LogInformation(
                "[Step: Quiz] Starting AI quiz generation for toolbox talk {Id}. MinQuestions={MinQuestions}, HasFinalPortion={HasFinalPortion}",
                toolboxTalkId, options.MinimumQuestions, !string.IsNullOrEmpty(extractionResult.VideoContent?.FinalPortionTranscript));

            progress?.Report(new ContentGenerationProgress(
                "GeneratingQuiz", 65, "AI is generating quiz questions..."));

            var videoFinalPortion = extractionResult.VideoContent?.FinalPortionTranscript;

            var quizResult = await _quizService.GenerateQuizAsync(
                toolboxTalkId,
                extractionResult.CombinedContent!,
                videoFinalPortion,
                extractionResult.VideoContent != null,
                extractionResult.PdfContent != null,
                options.MinimumQuestions,
                cancellationToken);

            totalTokens += quizResult.TokensUsed;
            hasFinalPortionQuestion = quizResult.HasFinalPortionQuestion;

            if (!quizResult.Success)
            {
                _logger.LogError(
                    "[Step: Quiz] FAILED - Quiz generation failed for toolbox talk {Id}. " +
                    "Error: {Error}, TokensUsed: {Tokens}",
                    toolboxTalkId, quizResult.ErrorMessage, quizResult.TokensUsed);
                errors.Add($"Quiz generation failed: {quizResult.ErrorMessage}");
            }
            else
            {
                questionsGenerated = quizResult.Questions.Count;
                _logger.LogInformation(
                    "[Step: Quiz] SUCCESS - Generated {Count} quiz questions for toolbox talk {Id}. " +
                    "HasFinalPortionQuestion: {HasFinal}, TokensUsed: {Tokens}",
                    questionsGenerated, toolboxTalkId, hasFinalPortionQuestion, quizResult.TokensUsed);

                foreach (var question in quizResult.Questions)
                {
                    _logger.LogDebug(
                        "[Step: Quiz] Question {SortOrder}: '{QuestionText}' (Source: {Source}, IsFromFinal: {IsFromFinal}, Options: {OptionCount})",
                        question.SortOrder,
                        question.QuestionText.Length > 50 ? question.QuestionText[..50] + "..." : question.QuestionText,
                        question.Source,
                        question.IsFromVideoFinalPortion,
                        question.Options.Count);
                }

                if (options.IncludeVideo && !hasFinalPortionQuestion)
                {
                    _logger.LogWarning(
                        "[Step: Quiz] WARNING - Could not generate a final portion question for toolbox talk {Id}. " +
                        "Users may skip to the end without consequence.",
                        toolboxTalkId);
                    warnings.Add("Could not generate a question from the video's final portion. Users may skip to the end without consequence.");
                }
            }

            progress?.Report(new ContentGenerationProgress(
                "GeneratingQuiz", 85, $"Generated {questionsGenerated} quiz questions."));

            // Stage 4: Save to database (85-100%)
            _logger.LogInformation(
                "[Step: Save] Starting database save for toolbox talk {Id}. " +
                "Sections: {SectionCount}, Questions: {QuestionCount}, ReplaceExisting: {Replace}",
                toolboxTalkId, sectionResult.Sections.Count, quizResult.Questions.Count, options.ReplaceExisting);

            progress?.Report(new ContentGenerationProgress(
                "Saving", 88, "Saving generated content to database..."));

            await SaveGeneratedContentAsync(
                toolboxTalk,
                sectionResult.Sections,
                quizResult.Questions,
                options,
                cancellationToken);

            // Update toolbox talk metadata
            var newStatus = errors.Count == 0 ? ToolboxTalkStatus.ReadyForReview : ToolboxTalkStatus.Draft;
            toolboxTalk.Status = newStatus;
            toolboxTalk.GeneratedFromVideo = options.IncludeVideo && extractionResult.VideoContent != null;
            toolboxTalk.GeneratedFromPdf = options.IncludePdf && extractionResult.PdfContent != null;
            toolboxTalk.RequiresQuiz = questionsGenerated > 0;
            toolboxTalk.PassingScore = options.PassThreshold;
            toolboxTalk.ContentGeneratedAt = DateTime.UtcNow; // Track when content was generated for deduplication

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[Step: Save] SUCCESS - Database save completed. " +
                "NewStatus: {Status}, GeneratedFromVideo: {FromVideo}, GeneratedFromPdf: {FromPdf}",
                newStatus, toolboxTalk.GeneratedFromVideo, toolboxTalk.GeneratedFromPdf);

            progress?.Report(new ContentGenerationProgress(
                "Complete", 100, "Content generation complete!"));

            // Determine final success status
            var success = errors.Count == 0 && (sectionsGenerated > 0 || questionsGenerated > 0);
            var partialSuccess = success && (extractionWasPartial || warnings.Count > 0);

            _logger.LogInformation(
                "[ContentGenerationService] Content generation complete for toolbox talk {Id}. " +
                "Final Results: Success={Success}, PartialSuccess={PartialSuccess}, Sections={Sections}, " +
                "Questions={Questions}, TotalTokens={Tokens}, Errors={ErrorCount}, Warnings={WarningCount}",
                toolboxTalkId, success, partialSuccess, sectionsGenerated, questionsGenerated, totalTokens, errors.Count, warnings.Count);

            return new ContentGenerationResult(
                Success: success,
                PartialSuccess: partialSuccess,
                SectionsGenerated: sectionsGenerated,
                QuestionsGenerated: questionsGenerated,
                HasFinalPortionQuestion: hasFinalPortionQuestion,
                Errors: errors,
                Warnings: warnings,
                TotalTokensUsed: totalTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ContentGenerationService] EXCEPTION - Content generation failed unexpectedly for toolbox talk {Id}. " +
                "ExceptionType: {ExceptionType}, Message: {Message}, " +
                "SectionsGenerated: {Sections}, QuestionsGenerated: {Questions}, TokensSoFar: {Tokens}",
                toolboxTalkId,
                ex.GetType().FullName,
                ex.Message,
                sectionsGenerated,
                questionsGenerated,
                totalTokens);

            // Try to reset status
            try
            {
                _logger.LogDebug("[Cleanup] Attempting to reset status to Draft for toolbox talk {Id}", toolboxTalkId);

                var toolboxTalk = await _dbContext.ToolboxTalks
                    .FirstOrDefaultAsync(t => t.Id == toolboxTalkId && t.TenantId == tenantId, cancellationToken);

                if (toolboxTalk != null)
                {
                    toolboxTalk.Status = ToolboxTalkStatus.Draft;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogDebug("[Cleanup] Successfully reset status to Draft for toolbox talk {Id}", toolboxTalkId);
                }
                else
                {
                    _logger.LogWarning("[Cleanup] Could not find toolbox talk {Id} for status reset", toolboxTalkId);
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx,
                    "[Cleanup] Failed to reset status after error for toolbox talk {Id}. CleanupError: {CleanupError}",
                    toolboxTalkId, cleanupEx.Message);
            }

            errors.Add(GetUserFriendlyError(ex.Message));
            return new ContentGenerationResult(
                Success: false,
                PartialSuccess: false,
                SectionsGenerated: sectionsGenerated,
                QuestionsGenerated: questionsGenerated,
                HasFinalPortionQuestion: hasFinalPortionQuestion,
                Errors: errors,
                Warnings: warnings,
                TotalTokensUsed: totalTokens);
        }
    }

    /// <summary>
    /// Converts technical error messages to user-friendly messages.
    /// </summary>
    private static string GetUserFriendlyError(string technicalError)
    {
        if (string.IsNullOrEmpty(technicalError))
            return "An unexpected error occurred. Please try again.";

        var lowerError = technicalError.ToLowerInvariant();

        if (lowerError.Contains("timeout") || lowerError.Contains("timed out"))
            return "The operation took too long. Please try again.";

        if (lowerError.Contains("network") || lowerError.Contains("connection"))
            return "A network error occurred. Please check your connection and try again.";

        if (lowerError.Contains("cancelled") || lowerError.Contains("canceled"))
            return "The operation was cancelled.";

        if (lowerError.Contains("api") && (lowerError.Contains("key") || lowerError.Contains("unauthorized")))
            return "AI service configuration error. Please contact support.";

        if (lowerError.Contains("rate limit") || lowerError.Contains("too many requests"))
            return "Service is temporarily busy. Please wait a moment and try again.";

        // Default: return the technical message but trimmed
        return technicalError.Length > 200 ? technicalError[..200] + "..." : technicalError;
    }

    /// <summary>
    /// Saves the generated sections and questions to the database.
    /// Handles both replace and append modes.
    /// </summary>
    private async Task SaveGeneratedContentAsync(
        ToolboxTalk toolboxTalk,
        List<GeneratedSection> sections,
        List<GeneratedQuizQuestion> questions,
        ContentGenerationOptions options,
        CancellationToken cancellationToken)
    {
        var currentTime = DateTime.UtcNow;
        var currentUser = _currentUser.UserId ?? "System";

        if (options.ReplaceExisting)
        {
            // Remove existing sections and questions (soft delete)
            foreach (var section in toolboxTalk.Sections.ToList())
            {
                section.IsDeleted = true;
                section.UpdatedAt = currentTime;
                section.UpdatedBy = currentUser;
            }

            foreach (var question in toolboxTalk.Questions.ToList())
            {
                question.IsDeleted = true;
                question.UpdatedAt = currentTime;
                question.UpdatedBy = currentUser;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Calculate starting sort orders
        var startingSectionOrder = options.ReplaceExisting
            ? 1
            : (toolboxTalk.Sections.Where(s => !s.IsDeleted).MaxBy(s => s.SectionNumber)?.SectionNumber ?? 0) + 1;

        var startingQuestionOrder = options.ReplaceExisting
            ? 1
            : (toolboxTalk.Questions.Where(q => !q.IsDeleted).MaxBy(q => q.QuestionNumber)?.QuestionNumber ?? 0) + 1;

        // Save sections
        foreach (var section in sections)
        {
            var entity = new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalk.Id,
                SectionNumber = startingSectionOrder + section.SortOrder - 1,
                Title = section.Title,
                Content = section.Content,
                Source = section.Source,
                VideoTimestamp = null,
                RequiresAcknowledgment = true,
                CreatedAt = currentTime,
                CreatedBy = currentUser
            };

            _dbContext.ToolboxTalkSections.Add(entity);
        }

        // Save questions
        foreach (var question in questions)
        {
            var optionsJson = JsonSerializer.Serialize(question.Options);

            var entity = new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalk.Id,
                QuestionNumber = startingQuestionOrder + question.SortOrder - 1,
                QuestionText = question.QuestionText,
                QuestionType = QuestionType.MultipleChoice,
                Options = optionsJson,
                CorrectAnswer = question.Options[question.CorrectAnswerIndex],
                CorrectOptionIndex = question.CorrectAnswerIndex,
                Points = 1,
                Source = question.Source,
                IsFromVideoFinalPortion = question.IsFromVideoFinalPortion,
                VideoTimestamp = question.VideoTimestamp,
                CreatedAt = currentTime,
                CreatedBy = currentUser
            };

            _dbContext.ToolboxTalkQuestions.Add(entity);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Saved {SectionCount} sections and {QuestionCount} questions for toolbox talk {Id}",
            sections.Count, questions.Count, toolboxTalk.Id);
    }
}
