using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.ContentCreation;

/// <summary>
/// Manages the lifecycle of content creation wizard sessions
/// </summary>
public class ContentCreationSessionService : IContentCreationSessionService
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IContentParserService _parserService;
    private readonly IR2StorageService _storageService;
    private readonly IAiQuizGenerationService _aiQuizService;
    private readonly IPdfExtractionService _pdfExtractionService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly TranslationValidationSettings _validationSettings;
    private readonly ILogger<ContentCreationSessionService> _logger;

    private static readonly JsonSerializerOptions CamelCaseJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "for", "in", "to", "of", "on", "at", "by", "with", "from", "is", "it"
    };

    public ContentCreationSessionService(
        IToolboxTalksDbContext dbContext,
        IContentParserService parserService,
        IR2StorageService storageService,
        IAiQuizGenerationService aiQuizService,
        IPdfExtractionService pdfExtractionService,
        ITranscriptionService transcriptionService,
        IOptions<TranslationValidationSettings> validationSettings,
        ILogger<ContentCreationSessionService> logger)
    {
        _dbContext = dbContext;
        _parserService = parserService;
        _storageService = storageService;
        _aiQuizService = aiQuizService;
        _pdfExtractionService = pdfExtractionService;
        _transcriptionService = transcriptionService;
        _validationSettings = validationSettings.Value;
        _logger = logger;
    }

    public async Task<ContentCreationSessionDto> CreateSessionAsync(
        CreateSessionRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var expiryHours = _validationSettings.SessionExpiryHours;

        var session = new ContentCreationSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InputMode = request.InputMode,
            Status = ContentCreationSessionStatus.Draft,
            SourceText = request.InputMode == InputMode.Text ? request.SourceText : null,
            PassThreshold = request.PassThreshold,
            SectorKey = request.SectorKey,
            ReviewerName = request.ReviewerName,
            ReviewerOrg = request.ReviewerOrg,
            ReviewerRole = request.ReviewerRole,
            DocumentRef = request.DocumentRef,
            ClientName = request.ClientName,
            AuditPurpose = request.AuditPurpose,
            ExpiresAt = DateTime.UtcNow.AddHours(expiryHours)
        };

        _dbContext.ContentCreationSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ContentCreationSession] Created session {SessionId} (mode: {Mode}, tenant: {TenantId})",
            session.Id, request.InputMode, tenantId);

        return MapToDto(session);
    }

    public async Task<ContentCreationSessionDto> UploadFileAsync(
        Guid sessionId,
        IFormFile file,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (session.Status != ContentCreationSessionStatus.Draft)
            throw new InvalidOperationException("File can only be uploaded in Draft status");

        if (session.InputMode == InputMode.Text)
            throw new InvalidOperationException("File upload is not allowed for Text input mode");

        if (file == null || file.Length == 0)
            throw new InvalidOperationException("No file provided");

        // Determine content type
        var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
        var contentType = file.ContentType;

        await using var stream = file.OpenReadStream();
        var result = await _storageService.UploadSessionFileAsync(
            tenantId, sessionId, stream, file.FileName, contentType, cancellationToken);

        if (!result.Success)
            throw new InvalidOperationException($"File upload failed: {result.ErrorMessage}");

        session.SourceFileName = file.FileName;
        session.SourceFileUrl = result.PublicUrl;
        session.SourceFileType = extension;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ContentCreationSession] File uploaded for session {SessionId}: {FileName}",
            sessionId, file.FileName);

        return MapToDto(session);
    }

    public async Task<ContentCreationSessionDto> UpdateSourceAsync(
        Guid sessionId,
        UpdateSourceRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (session.InputMode == InputMode.Text)
        {
            session.SourceText = request.SourceText;
        }

        // Reset to Draft so parse can be triggered again
        session.Status = ContentCreationSessionStatus.Draft;
        session.ParsedSectionsJson = null;
        session.OutputType = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ContentCreationSession] Source updated and reset to Draft for session {SessionId}",
            sessionId);

        return MapToDto(session);
    }

    public async Task<ContentCreationSessionDto> ParseContentAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (session.Status != ContentCreationSessionStatus.Draft)
            throw new InvalidOperationException("Content can only be parsed in Draft status");

        // PDF mode: extract text from uploaded PDF if not already transcribed
        if (session.InputMode == InputMode.Pdf && string.IsNullOrWhiteSpace(session.TranscriptText))
        {
            if (string.IsNullOrWhiteSpace(session.SourceFileUrl))
                throw new InvalidOperationException("No PDF file uploaded. Upload a PDF first.");

            _logger.LogInformation(
                "[ContentCreationSession] Extracting text from PDF for session {SessionId}, URL: {Url}",
                sessionId, session.SourceFileUrl);

            var pdfResult = await _pdfExtractionService.ExtractTextFromUrlAsync(
                session.SourceFileUrl, cancellationToken);

            if (string.IsNullOrWhiteSpace(pdfResult.Text))
                throw new InvalidOperationException("PDF text extraction returned no content. The PDF may be scanned or image-based.");

            session.TranscriptText = pdfResult.Text;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[ContentCreationSession] PDF extraction complete for session {SessionId}: {PageCount} pages, {Length} chars",
                sessionId, pdfResult.PageCount, pdfResult.Text.Length);
        }

        // Video mode: transcribe video if not already transcribed
        if (session.InputMode == InputMode.Video && string.IsNullOrWhiteSpace(session.TranscriptText))
        {
            if (string.IsNullOrWhiteSpace(session.SourceFileUrl))
                throw new InvalidOperationException("No video file uploaded. Upload a video first.");

            _logger.LogInformation(
                "[ContentCreationSession] Transcribing video for session {SessionId}, URL: {Url}",
                sessionId, session.SourceFileUrl);

            var transcriptionResult = await _transcriptionService.TranscribeAsync(
                session.SourceFileUrl, cancellationToken);

            if (!transcriptionResult.Success)
                throw new InvalidOperationException($"Video transcription failed: {transcriptionResult.ErrorMessage}");

            var transcriptText = string.Join(" ", transcriptionResult.Words
                .Where(w => w.Type == "word")
                .Select(w => w.Text));

            if (string.IsNullOrWhiteSpace(transcriptText))
                throw new InvalidOperationException("Video transcription returned no content.");

            session.TranscriptText = transcriptText;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[ContentCreationSession] Video transcription complete for session {SessionId}: {WordCount} words, {Length} chars",
                sessionId, transcriptionResult.Words.Count(w => w.Type == "word"), transcriptText.Length);
        }

        // Determine raw text to parse
        var rawText = session.InputMode switch
        {
            InputMode.Text => session.SourceText,
            InputMode.Video => session.TranscriptText,
            InputMode.Pdf => session.TranscriptText ?? session.SourceText,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(rawText))
            throw new InvalidOperationException("No content available to parse. Upload a file or provide text first.");

        session.Status = ContentCreationSessionStatus.Parsing;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var parseResult = await _parserService.ParseContentAsync(
                rawText, session.InputMode, cancellationToken);

            if (!parseResult.Success)
            {
                session.Status = ContentCreationSessionStatus.Failed;
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException($"Parsing failed: {parseResult.ErrorMessage}");
            }

            session.ParsedSectionsJson = JsonSerializer.Serialize(parseResult.Sections, CamelCaseJson);
            session.OutputType = parseResult.SuggestedOutputType;
            session.Status = ContentCreationSessionStatus.Parsed;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[ContentCreationSession] Parsed {Count} sections for session {SessionId}, suggested: {OutputType}",
                parseResult.Sections.Count, sessionId, parseResult.SuggestedOutputType);

            return MapToDto(session);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            session.Status = ContentCreationSessionStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "[ContentCreationSession] Parse failed for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<ContentCreationSessionDto> UpdateSectionsAsync(
        Guid sessionId,
        UpdateSectionsRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (session.Status != ContentCreationSessionStatus.Parsed &&
            session.Status != ContentCreationSessionStatus.Validated)
            throw new InvalidOperationException("Sections can only be updated in Parsed or Validated status");

        var sections = request.Sections
            .Select(s => new ParsedSection(s.Title, s.Content, s.Order))
            .OrderBy(s => s.SuggestedOrder)
            .ToList();

        session.ParsedSectionsJson = JsonSerializer.Serialize(sections, CamelCaseJson);
        session.OutputType = request.OutputType;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ContentCreationSession] Sections updated for session {SessionId}: {Count} sections, output: {OutputType}",
            sessionId, sections.Count, request.OutputType);

        return MapToDto(session);
    }

    public async Task<ContentCreationSessionDto> StartTranslateValidateAsync(
        Guid sessionId,
        StartTranslateValidateRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (session.Status != ContentCreationSessionStatus.Parsed &&
            session.Status != ContentCreationSessionStatus.Validated &&
            session.Status != ContentCreationSessionStatus.QuizGenerated)
            throw new InvalidOperationException("Translation/validation can only start from Parsed, QuizGenerated, or Validated status");

        if (request.TargetLanguageCodes.Count == 0)
            throw new InvalidOperationException("At least one target language code is required");

        if (string.IsNullOrWhiteSpace(session.ParsedSectionsJson))
            throw new InvalidOperationException("No parsed sections available for validation");

        // Parse the sections from the session
        var sections = JsonSerializer.Deserialize<List<ParsedSection>>(session.ParsedSectionsJson, CamelCaseJson) ?? new();
        if (sections.Count == 0)
            throw new InvalidOperationException("No sections available for validation");

        // Parse settings from session (title, description, category)
        SessionSettingsDto? sessionSettings = null;
        if (!string.IsNullOrWhiteSpace(session.SettingsJson))
            sessionSettings = JsonSerializer.Deserialize<SessionSettingsDto>(session.SettingsJson, CamelCaseJson);

        // Parse quiz questions and settings from session
        List<SessionQuizQuestionDto>? quizQuestions = null;
        if (!string.IsNullOrWhiteSpace(session.QuestionsJson))
            quizQuestions = JsonSerializer.Deserialize<List<SessionQuizQuestionDto>>(session.QuestionsJson, CamelCaseJson);

        SessionQuizSettingsDto? quizSettings = null;
        if (!string.IsNullOrWhiteSpace(session.QuizSettingsJson))
            quizSettings = JsonSerializer.Deserialize<SessionQuizSettingsDto>(session.QuizSettingsJson, CamelCaseJson);

        // Derive the talk title from settings or fallback
        var talkTitle = !string.IsNullOrWhiteSpace(sessionSettings?.Title)
            ? sessionSettings.Title
            : $"[Draft] Session {sessionId.ToString()[..8]}";
        var talkDescription = sessionSettings?.Description;
        var talkCategory = sessionSettings?.Category;

        // Create or reuse a draft ToolboxTalk so the validation job can load sections
        Guid talkId;
        ToolboxTalk? newDraftTalk = null;
        if (session.OutputTalkId.HasValue)
        {
            // Re-running validation on existing draft — reuse the talk
            talkId = session.OutputTalkId.Value;

            // Update the talk metadata from session settings and quiz
            var existingTalk = await _dbContext.ToolboxTalks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == talkId && !t.IsDeleted, cancellationToken);

            if (existingTalk != null)
            {
                existingTalk.Title = talkTitle;
                existingTalk.Description = talkDescription;
                existingTalk.Category = talkCategory;
                SyncQuizSettingsToTalk(existingTalk, quizSettings);
            }

            // Update sections in case they changed (user edited in Step 2)
            var existingSections = await _dbContext.ToolboxTalkSections
                .Where(s => s.ToolboxTalkId == talkId)
                .ToListAsync(cancellationToken);

            // Remove old sections
            foreach (var existing in existingSections)
                existing.IsDeleted = true;

            // Add updated sections
            foreach (var (parsed, i) in sections.Select((s, i) => (s, i)))
            {
                _dbContext.ToolboxTalkSections.Add(new ToolboxTalkSection
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = talkId,
                    SectionNumber = parsed.SuggestedOrder,
                    Title = parsed.Title,
                    Content = parsed.Content,
                    RequiresAcknowledgment = true
                });
            }

            // Remove old questions so they are recreated from session quiz data
            var existingQuestions = await _dbContext.ToolboxTalkQuestions
                .Where(q => q.ToolboxTalkId == talkId)
                .ToListAsync(cancellationToken);
            foreach (var q in existingQuestions)
                _dbContext.ToolboxTalkQuestions.Remove(q);

            // Add quiz questions from session
            SyncQuizQuestionsToTalk(talkId, quizQuestions, session.InputMode);

            // Remove old translations so they are regenerated
            var existingTranslations = await _dbContext.ToolboxTalkTranslations
                .Where(t => t.ToolboxTalkId == talkId)
                .ToListAsync(cancellationToken);
            foreach (var t in existingTranslations)
                t.IsDeleted = true;
        }
        else
        {
            // Create a draft talk from session sections with full metadata
            var draftCode = await GenerateCodeAsync(talkTitle, tenantId, cancellationToken);
            newDraftTalk = new ToolboxTalk
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = draftCode,
                Title = talkTitle,
                Description = talkDescription,
                Category = talkCategory,
                Status = ToolboxTalkStatus.Draft,
                SourceLanguageCode = "en",
                IsActive = false
            };

            SyncQuizSettingsToTalk(newDraftTalk, quizSettings);

            foreach (var (parsed, i) in sections.Select((s, i) => (s, i)))
            {
                newDraftTalk.Sections.Add(new ToolboxTalkSection
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = newDraftTalk.Id,
                    SectionNumber = parsed.SuggestedOrder,
                    Title = parsed.Title,
                    Content = parsed.Content,
                    RequiresAcknowledgment = true
                });
            }

            // Add quiz questions from session
            if (quizQuestions != null)
            {
                var source = session.InputMode switch
                {
                    InputMode.Video => ContentSource.Video,
                    InputMode.Pdf => ContentSource.Pdf,
                    _ => ContentSource.Manual
                };

                var questionNumber = 1;
                foreach (var q in quizQuestions)
                {
                    var options = q.Options;
                    var correctAnswer = q.CorrectAnswerIndex >= 0 && q.CorrectAnswerIndex < options.Count
                        ? options[q.CorrectAnswerIndex]
                        : string.Empty;

                    newDraftTalk.Questions.Add(new ToolboxTalkQuestion
                    {
                        Id = Guid.NewGuid(),
                        ToolboxTalkId = newDraftTalk.Id,
                        QuestionNumber = questionNumber++,
                        QuestionText = q.QuestionText,
                        QuestionType = Enum.TryParse<QuestionType>(q.QuestionType, out var qt) ? qt : QuestionType.MultipleChoice,
                        Options = JsonSerializer.Serialize(options),
                        CorrectAnswer = correctAnswer,
                        CorrectOptionIndex = q.CorrectAnswerIndex,
                        Points = q.Points,
                        Source = source
                    });
                }
            }

            _dbContext.ToolboxTalks.Add(newDraftTalk);
            talkId = newDraftTalk.Id;
            session.OutputTalkId = talkId;
        }

        session.TargetLanguageCodes = JsonSerializer.Serialize(request.TargetLanguageCodes);
        session.Status = ContentCreationSessionStatus.TranslatingValidating;

        // Create a TranslationValidationRun per language and enqueue Hangfire jobs
        var runIds = new List<Guid>();
        foreach (var langCode in request.TargetLanguageCodes)
        {
            var run = new TranslationValidationRun
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ToolboxTalkId = talkId,
                LanguageCode = langCode,
                SectorKey = session.SectorKey,
                PassThreshold = session.PassThreshold,
                SourceLanguage = "en",
                ReviewerName = session.ReviewerName,
                ReviewerOrg = session.ReviewerOrg,
                ReviewerRole = session.ReviewerRole,
                DocumentRef = session.DocumentRef,
                ClientName = session.ClientName,
                AuditPurpose = session.AuditPurpose,
                Status = ValidationRunStatus.Pending
            };

            _dbContext.TranslationValidationRuns.Add(run);
            runIds.Add(run.Id);
        }

        session.ValidationRunIds = JsonSerializer.Serialize(runIds);
        if (newDraftTalk != null)
        {
            await SaveWithCodeRetryAsync(
                () => [newDraftTalk],
                tenantId,
                cancellationToken);
        }
        else
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // Enqueue Hangfire jobs after save
        foreach (var runId in runIds)
        {
            BackgroundJob.Enqueue<TranslationValidationJob>(
                job => job.ExecuteAsync(runId, tenantId, CancellationToken.None));
        }

        _logger.LogInformation(
            "[ContentCreationSession] Started translate+validate for session {SessionId}: {Count} languages, TalkId: {TalkId}",
            sessionId, request.TargetLanguageCodes.Count, talkId);

        return MapToDto(session);
    }

    public async Task<ContentCreationSessionDto> GetSessionAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);
        return MapToDto(session);
    }

    public async Task AbandonSessionAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        session.Status = ContentCreationSessionStatus.Abandoned;
        session.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Clean up R2 files
        if (!string.IsNullOrEmpty(session.SourceFileUrl))
        {
            try
            {
                await _storageService.DeleteSessionFilesAsync(tenantId, sessionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[ContentCreationSession] Failed to clean up R2 files for abandoned session {SessionId}",
                    sessionId);
            }
        }

        _logger.LogInformation("[ContentCreationSession] Session {SessionId} abandoned", sessionId);
    }

    public async Task<PublishResult> PublishAsync(
        Guid sessionId,
        PublishRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (session.Status != ContentCreationSessionStatus.Parsed &&
            session.Status != ContentCreationSessionStatus.Validated &&
            session.Status != ContentCreationSessionStatus.QuizGenerated)
            throw new InvalidOperationException("Session must be in Parsed, Validated, or QuizGenerated status to publish");

        if (session.OutputType == null)
            throw new InvalidOperationException("Output type must be set before publishing");

        if (string.IsNullOrWhiteSpace(session.ParsedSectionsJson))
            throw new InvalidOperationException("No parsed sections available for publishing");

        // Fall back to session settings for missing request fields (title, description, category)
        if (string.IsNullOrWhiteSpace(request.Title) && !string.IsNullOrWhiteSpace(session.SettingsJson))
        {
            var sessionSettings = JsonSerializer.Deserialize<SessionSettingsDto>(session.SettingsJson, CamelCaseJson);
            if (sessionSettings != null)
            {
                request = request with
                {
                    Title = !string.IsNullOrWhiteSpace(sessionSettings.Title) ? sessionSettings.Title : request.Title,
                    Description = request.Description ?? sessionSettings.Description,
                    Category = request.Category ?? sessionSettings.Category
                };
            }
        }

        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("A title is required to publish. Set a title in the Settings step.");

        session.Status = ContentCreationSessionStatus.Publishing;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var sections = JsonSerializer.Deserialize<List<ParsedSection>>(session.ParsedSectionsJson, CamelCaseJson)
                ?? new List<ParsedSection>();

            if (session.OutputType == OutputType.Lesson)
            {
                var outputId = await PublishAsLessonAsync(
                    session, sections, request, tenantId, cancellationToken);
                session.OutputTalkId = outputId;
            }
            else
            {
                var outputId = await PublishAsCourseAsync(
                    session, sections, request, tenantId, cancellationToken);
                session.OutputCourseId = outputId;
            }

            session.Status = ContentCreationSessionStatus.Completed;
            await _dbContext.SaveChangesAsync(cancellationToken);

            var effectiveOutputId = session.OutputType == OutputType.Lesson
                ? session.OutputTalkId
                : session.OutputCourseId;

            _logger.LogInformation(
                "[ContentCreationSession] Published session {SessionId} as {OutputType} {OutputId}",
                sessionId, session.OutputType, effectiveOutputId);

            return new PublishResult(true, effectiveOutputId, session.OutputType);
        }
        catch (Exception ex)
        {
            ((DbContext)_dbContext).ChangeTracker.Clear();
            var failedSession = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);
            failedSession.Status = ContentCreationSessionStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "[ContentCreationSession] Publish failed for session {SessionId}", sessionId);
            return new PublishResult(false, null, null, "An error occurred while publishing. Please try again.");
        }
    }

    public async Task<ContentCreationSessionDto> GenerateQuizAsync(
        Guid sessionId,
        Guid tenantId,
        int minimumQuestionsPerSection = 2,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (session.Status != ContentCreationSessionStatus.Validated &&
            session.Status != ContentCreationSessionStatus.Parsed &&
            session.Status != ContentCreationSessionStatus.QuizGenerated)
            throw new InvalidOperationException("Quiz can only be generated from Parsed, Validated, or QuizGenerated status");

        if (string.IsNullOrWhiteSpace(session.ParsedSectionsJson))
            throw new InvalidOperationException("No parsed sections available for quiz generation");

        var sections = JsonSerializer.Deserialize<List<ParsedSection>>(session.ParsedSectionsJson, CamelCaseJson) ?? new();
        if (sections.Count == 0)
            throw new InvalidOperationException("No sections found");

        session.Status = ContentCreationSessionStatus.GeneratingQuiz;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var allQuestions = new List<SessionQuizQuestionDto>();
            var questionId = 0;

            // Generate questions per section for natural grouping
            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                var sectionContent = $"Section: {section.Title}\n\n{section.Content}";

                var result = await _aiQuizService.GenerateQuizAsync(
                    sessionId,
                    sectionContent,
                    videoFinalPortionContent: null,
                    hasVideoContent: session.InputMode == Domain.Enums.InputMode.Video,
                    hasPdfContent: session.InputMode == Domain.Enums.InputMode.Pdf,
                    minimumQuestions: minimumQuestionsPerSection,
                    cancellationToken);

                if (result.Success && result.Questions.Count > 0)
                {
                    foreach (var q in result.Questions)
                    {
                        allQuestions.Add(new SessionQuizQuestionDto
                        {
                            Id = Guid.NewGuid().ToString(),
                            SectionIndex = i,
                            QuestionText = q.QuestionText,
                            QuestionType = "MultipleChoice",
                            Options = q.Options,
                            CorrectAnswerIndex = q.CorrectAnswerIndex,
                            Points = 1,
                            IsAiGenerated = true
                        });
                        questionId++;
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "[ContentCreationSession] Quiz generation produced no questions for section {Index} of session {SessionId}",
                        i, sessionId);
                }
            }

            session.QuestionsJson = JsonSerializer.Serialize(allQuestions, CamelCaseJson);

            // Initialize default quiz settings if not already set
            if (string.IsNullOrWhiteSpace(session.QuizSettingsJson))
            {
                var defaultSettings = new SessionQuizSettingsDto
                {
                    RequireQuiz = true,
                    PassingScore = 80,
                    ShuffleQuestions = false,
                    ShuffleOptions = false,
                    AllowRetry = true
                };
                session.QuizSettingsJson = JsonSerializer.Serialize(defaultSettings, CamelCaseJson);
            }

            session.Status = ContentCreationSessionStatus.QuizGenerated;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[ContentCreationSession] Generated {Count} quiz questions for session {SessionId}",
                allQuestions.Count, sessionId);

            return MapToDto(session);
        }
        catch (Exception ex)
        {
            session.Status = ContentCreationSessionStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "[ContentCreationSession] Quiz generation failed for session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<SessionQuizDataDto> GetQuizDataAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        var questions = !string.IsNullOrWhiteSpace(session.QuestionsJson)
            ? JsonSerializer.Deserialize<List<SessionQuizQuestionDto>>(session.QuestionsJson, CamelCaseJson) ?? new()
            : new List<SessionQuizQuestionDto>();

        var settings = !string.IsNullOrWhiteSpace(session.QuizSettingsJson)
            ? JsonSerializer.Deserialize<SessionQuizSettingsDto>(session.QuizSettingsJson, CamelCaseJson) ?? new()
            : new SessionQuizSettingsDto();

        return new SessionQuizDataDto
        {
            Questions = questions,
            Settings = settings
        };
    }

    public async Task<ContentCreationSessionDto> UpdateQuestionsAsync(
        Guid sessionId,
        UpdateSessionQuestionsRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (session.Status != ContentCreationSessionStatus.QuizGenerated &&
            session.Status != ContentCreationSessionStatus.Validated &&
            session.Status != ContentCreationSessionStatus.Parsed)
            throw new InvalidOperationException("Questions can only be updated in QuizGenerated, Validated, or Parsed status");

        session.QuestionsJson = JsonSerializer.Serialize(request.Questions, CamelCaseJson);

        // If questions are being set for the first time (no prior generation), move to QuizGenerated
        if (session.Status != ContentCreationSessionStatus.QuizGenerated)
            session.Status = ContentCreationSessionStatus.QuizGenerated;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ContentCreationSession] Updated {Count} questions for session {SessionId}",
            request.Questions.Count, sessionId);

        return MapToDto(session);
    }

    public async Task<ContentCreationSessionDto> UpdateQuizSettingsAsync(
        Guid sessionId,
        SessionQuizSettingsDto settings,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        session.QuizSettingsJson = JsonSerializer.Serialize(settings, CamelCaseJson);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ContentCreationSession] Updated quiz settings for session {SessionId}", sessionId);

        return MapToDto(session);
    }

    public async Task<SessionSettingsDto> GetSettingsAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(session.SettingsJson))
            return JsonSerializer.Deserialize<SessionSettingsDto>(session.SettingsJson, CamelCaseJson) ?? new();

        // Build defaults from parsed content
        var suggestedTitle = string.Empty;
        if (!string.IsNullOrWhiteSpace(session.ParsedSectionsJson))
        {
            try
            {
                var sections = JsonSerializer.Deserialize<List<ParsedSection>>(session.ParsedSectionsJson, CamelCaseJson);
                if (sections?.Count > 0)
                    suggestedTitle = sections[0].Title;
            }
            catch { /* ignore parse errors */ }
        }

        // Fall back to source file name (strip extension)
        if (string.IsNullOrWhiteSpace(suggestedTitle) && !string.IsNullOrWhiteSpace(session.SourceFileName))
            suggestedTitle = Path.GetFileNameWithoutExtension(session.SourceFileName);

        return new SessionSettingsDto { Title = suggestedTitle ?? string.Empty };
    }

    public async Task<ContentCreationSessionDto> UpdateSettingsAsync(
        Guid sessionId,
        SessionSettingsDto settings,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        session.SettingsJson = JsonSerializer.Serialize(settings, CamelCaseJson);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ContentCreationSession] Updated settings for session {SessionId}", sessionId);

        return MapToDto(session);
    }

    public async Task<ContentCreationSessionDto> UploadCoverImageAsync(
        Guid sessionId,
        IFormFile file,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (file == null || file.Length == 0)
            throw new InvalidOperationException("No file provided");

        if (file.Length > 5 * 1024 * 1024) // 5MB
            throw new InvalidOperationException("Cover image must be under 5MB");

        var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
        if (extension != "png" && extension != "jpg" && extension != "jpeg")
            throw new InvalidOperationException("Cover image must be PNG or JPG");

        await using var stream = file.OpenReadStream();
        var result = await _storageService.UploadSessionFileAsync(
            tenantId, sessionId, stream, $"cover.{extension}", file.ContentType, cancellationToken);

        if (!result.Success)
            throw new InvalidOperationException($"Cover image upload failed: {result.ErrorMessage}");

        // Update settings with the new cover image URL
        var settings = !string.IsNullOrWhiteSpace(session.SettingsJson)
            ? JsonSerializer.Deserialize<SessionSettingsDto>(session.SettingsJson, CamelCaseJson) ?? new()
            : new SessionSettingsDto();

        settings = settings with { CoverImageUrl = result.PublicUrl };
        session.SettingsJson = JsonSerializer.Serialize(settings, CamelCaseJson);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ContentCreationSession] Cover image uploaded for session {SessionId}", sessionId);

        return MapToDto(session);
    }

    #region Private Helpers

    private async Task<ContentCreationSession> GetSessionEntityAsync(
        Guid sessionId, Guid tenantId, CancellationToken cancellationToken)
    {
        var session = await _dbContext.ContentCreationSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.TenantId == tenantId, cancellationToken);

        if (session == null)
            throw new InvalidOperationException("Session not found");

        if (session.ExpiresAt < DateTime.UtcNow &&
            session.Status != ContentCreationSessionStatus.Completed &&
            session.Status != ContentCreationSessionStatus.Abandoned)
            throw new InvalidOperationException("Session has expired");

        return session;
    }

    private async Task<Guid> PublishAsLessonAsync(
        ContentCreationSession session,
        List<ParsedSection> sections,
        PublishRequest request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // If a draft talk already exists (created during StartTranslateValidateAsync),
        // promote it instead of creating a duplicate — translations and validation runs
        // are already linked to this draft talk ID.
        if (session.OutputTalkId.HasValue)
        {
            var draftTalk = await _dbContext.ToolboxTalks
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.Id == session.OutputTalkId.Value && !t.IsDeleted, cancellationToken);

            if (draftTalk != null)
            {
                // Validate title uniqueness (exclude the draft itself)
                var titleExists = await _dbContext.ToolboxTalks
                    .AnyAsync(t => t.TenantId == tenantId && t.Title == request.Title && t.Id != draftTalk.Id, cancellationToken);
                if (titleExists)
                    throw new InvalidOperationException($"A learning with title '{request.Title}' already exists.");

                draftTalk.Title = request.Title;
                draftTalk.Description = request.Description;
                draftTalk.Category = request.Category;
                draftTalk.SourceLanguageCode = request.SourceLanguageCode ?? "en";
                draftTalk.Status = ToolboxTalkStatus.Published;
                draftTalk.VideoUrl = session.InputMode == InputMode.Video ? session.SourceFileUrl : null;
                draftTalk.VideoSource = session.InputMode == InputMode.Video ? VideoSource.DirectUrl : VideoSource.None;
                draftTalk.PdfUrl = session.InputMode == InputMode.Pdf ? session.SourceFileUrl : null;
                draftTalk.PdfFileName = session.InputMode == InputMode.Pdf ? session.SourceFileName : null;

                // Apply behaviour fields from session settings
                SessionSettingsDto? courseSessionSettings = null;
                if (!string.IsNullOrWhiteSpace(session.SettingsJson))
                    courseSessionSettings = JsonSerializer.Deserialize<SessionSettingsDto>(session.SettingsJson, CamelCaseJson);

                draftTalk.IsActive = courseSessionSettings?.IsActiveOnPublish ?? true;
                draftTalk.GenerateCertificate = courseSessionSettings?.GenerateCertificate ?? false;
                draftTalk.MinimumVideoWatchPercent = courseSessionSettings?.MinimumWatchPercent ?? 90;
                draftTalk.AutoAssignToNewEmployees = courseSessionSettings?.AutoAssign ?? false;
                draftTalk.AutoAssignDueDays = courseSessionSettings?.AutoAssignDueDays ?? 14;

                if (courseSessionSettings != null && courseSessionSettings.RefresherFrequency != "Once")
                {
                    draftTalk.RequiresRefresher = true;
                    draftTalk.RefresherIntervalMonths = courseSessionSettings.RefresherFrequency switch
                    {
                        "Monthly" => 1,
                        "Quarterly" => 3,
                        "Annually" => 12,
                        _ => 12
                    };
                }
                else
                {
                    draftTalk.RequiresRefresher = false;
                }

                // Regenerate code if a custom one was requested
                if (!string.IsNullOrWhiteSpace(request.Code) && request.Code.Trim() != draftTalk.Code)
                {
                    var codeExists = await _dbContext.ToolboxTalks
                        .AnyAsync(t => t.TenantId == tenantId && t.Code == request.Code && t.Id != draftTalk.Id, cancellationToken);
                    if (codeExists)
                        throw new InvalidOperationException($"A learning with code '{request.Code}' already exists.");
                    draftTalk.Code = request.Code.Trim();
                }

                // Re-sync sections from session in case they were edited after validation
                var existingSections = await _dbContext.ToolboxTalkSections
                    .Where(s => s.ToolboxTalkId == draftTalk.Id)
                    .ToListAsync(cancellationToken);
                foreach (var existing in existingSections)
                    existing.IsDeleted = true;

                foreach (var parsedSection in sections)
                {
                    _dbContext.ToolboxTalkSections.Add(new ToolboxTalkSection
                    {
                        Id = Guid.NewGuid(),
                        ToolboxTalkId = draftTalk.Id,
                        SectionNumber = parsedSection.SuggestedOrder,
                        Title = parsedSection.Title,
                        Content = parsedSection.Content,
                        RequiresAcknowledgment = true
                    });
                }

                // Re-sync quiz settings from session
                SessionQuizSettingsDto? courseQuizSettings = null;
                if (!string.IsNullOrWhiteSpace(session.QuizSettingsJson))
                    courseQuizSettings = JsonSerializer.Deserialize<SessionQuizSettingsDto>(session.QuizSettingsJson, CamelCaseJson);
                SyncQuizSettingsToTalk(draftTalk, courseQuizSettings);

                // Re-sync quiz questions from session
                var existingQuestions = await _dbContext.ToolboxTalkQuestions
                    .Where(q => q.ToolboxTalkId == draftTalk.Id)
                    .ToListAsync(cancellationToken);
                foreach (var q in existingQuestions)
                    _dbContext.ToolboxTalkQuestions.Remove(q);

                List<SessionQuizQuestionDto>? courseQuizQuestions = null;
                if (!string.IsNullOrWhiteSpace(session.QuestionsJson))
                    courseQuizQuestions = JsonSerializer.Deserialize<List<SessionQuizQuestionDto>>(session.QuestionsJson, CamelCaseJson);
                SyncQuizQuestionsToTalk(draftTalk.Id, courseQuizQuestions, session.InputMode);

                await _dbContext.SaveChangesAsync(cancellationToken);
                return draftTalk.Id;
            }
        }

        // No draft exists — create a new talk (fallback path)
        var titleExistsNew = await _dbContext.ToolboxTalks
            .AnyAsync(t => t.TenantId == tenantId && t.Title == request.Title, cancellationToken);

        if (titleExistsNew)
            throw new InvalidOperationException($"A learning with title '{request.Title}' already exists.");

        // Generate or validate code (same pattern as CreateToolboxTalkCommandHandler)
        string code;
        if (!string.IsNullOrWhiteSpace(request.Code))
        {
            var codeExists = await _dbContext.ToolboxTalks
                .AnyAsync(t => t.TenantId == tenantId && t.Code == request.Code, cancellationToken);
            if (codeExists)
                throw new InvalidOperationException($"A learning with code '{request.Code}' already exists.");
            code = request.Code.Trim();
        }
        else
        {
            code = await GenerateCodeAsync(request.Title, tenantId, cancellationToken);
        }

        // Apply behaviour fields from session settings
        SessionSettingsDto? sessionSettings = null;
        if (!string.IsNullOrWhiteSpace(session.SettingsJson))
            sessionSettings = JsonSerializer.Deserialize<SessionSettingsDto>(session.SettingsJson, CamelCaseJson);

        var talk = new ToolboxTalk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            SourceLanguageCode = request.SourceLanguageCode,
            Status = ToolboxTalkStatus.Published,
            VideoUrl = session.InputMode == InputMode.Video ? session.SourceFileUrl : null,
            VideoSource = session.InputMode == InputMode.Video ? VideoSource.DirectUrl : VideoSource.None,
            PdfUrl = session.InputMode == InputMode.Pdf ? session.SourceFileUrl : null,
            PdfFileName = session.InputMode == InputMode.Pdf ? session.SourceFileName : null,
            IsActive = sessionSettings?.IsActiveOnPublish ?? true,
            GenerateCertificate = sessionSettings?.GenerateCertificate ?? false,
            MinimumVideoWatchPercent = sessionSettings?.MinimumWatchPercent ?? 90,
            AutoAssignToNewEmployees = sessionSettings?.AutoAssign ?? false,
            AutoAssignDueDays = sessionSettings?.AutoAssignDueDays ?? 14
        };

        if (sessionSettings != null && sessionSettings.RefresherFrequency != "Once")
        {
            talk.RequiresRefresher = true;
            talk.RefresherIntervalMonths = sessionSettings.RefresherFrequency switch
            {
                "Monthly" => 1,
                "Quarterly" => 3,
                "Annually" => 12,
                _ => 12
            };
        }

        // Create sections (same pattern as CreateToolboxTalkCommandHandler)
        foreach (var parsedSection in sections)
        {
            var section = new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = talk.Id,
                SectionNumber = parsedSection.SuggestedOrder,
                Title = parsedSection.Title,
                Content = parsedSection.Content,
                RequiresAcknowledgment = true
            };
            talk.Sections.Add(section);
        }

        // Sync quiz settings from session
        SessionQuizSettingsDto? quizSettings = null;
        if (!string.IsNullOrWhiteSpace(session.QuizSettingsJson))
            quizSettings = JsonSerializer.Deserialize<SessionQuizSettingsDto>(session.QuizSettingsJson, CamelCaseJson);
        SyncQuizSettingsToTalk(talk, quizSettings);

        // Sync quiz questions from session
        List<SessionQuizQuestionDto>? quizQuestions = null;
        if (!string.IsNullOrWhiteSpace(session.QuestionsJson))
            quizQuestions = JsonSerializer.Deserialize<List<SessionQuizQuestionDto>>(session.QuestionsJson, CamelCaseJson);
        SyncQuizQuestionsToTalk(talk.Id, quizQuestions, session.InputMode);

        _dbContext.ToolboxTalks.Add(talk);
        await SaveWithCodeRetryAsync(() => [talk], tenantId, cancellationToken);

        return talk.Id;
    }

    private async Task<Guid> PublishAsCourseAsync(
        ContentCreationSession session,
        List<ParsedSection> sections,
        PublishRequest request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Deserialize quiz data from session so each course talk gets quiz questions & settings
        List<SessionQuizQuestionDto>? quizQuestions = null;
        if (!string.IsNullOrWhiteSpace(session.QuestionsJson))
            quizQuestions = JsonSerializer.Deserialize<List<SessionQuizQuestionDto>>(session.QuestionsJson, CamelCaseJson);

        SessionQuizSettingsDto? quizSettings = null;
        if (!string.IsNullOrWhiteSpace(session.QuizSettingsJson))
            quizSettings = JsonSerializer.Deserialize<SessionQuizSettingsDto>(session.QuizSettingsJson, CamelCaseJson);

        // Validate title uniqueness (same pattern as CreateToolboxTalkCourseCommandHandler)
        var titleExists = await _dbContext.ToolboxTalkCourses
            .AnyAsync(c => c.TenantId == tenantId && c.Title == request.Title && !c.IsDeleted, cancellationToken);

        if (titleExists)
            throw new InvalidOperationException($"A course with title '{request.Title}' already exists.");

        // Load draft talk translations if a draft was created during StartTranslateValidateAsync
        List<ToolboxTalkTranslation>? draftTranslations = null;
        List<ToolboxTalkSection>? draftSections = null;
        if (session.OutputTalkId.HasValue)
        {
            draftSections = await _dbContext.ToolboxTalkSections
                .IgnoreQueryFilters()
                .Where(s => s.ToolboxTalkId == session.OutputTalkId.Value && !s.IsDeleted)
                .OrderBy(s => s.SectionNumber)
                .ToListAsync(cancellationToken);

            draftTranslations = await _dbContext.ToolboxTalkTranslations
                .IgnoreQueryFilters()
                .Where(t => t.ToolboxTalkId == session.OutputTalkId.Value && !t.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        var course = new ToolboxTalkCourse
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = request.Title,
            Description = request.Description,
            IsActive = true,
            RequireSequentialCompletion = true
        };

        // Create a Talk per section and add as course items
        var orderIndex = 0;
        var courseTalks = new List<ToolboxTalk>();
        foreach (var parsedSection in sections)
        {
            var talkCode = await GenerateCodeAsync(parsedSection.Title, tenantId, cancellationToken);

            var talk = new ToolboxTalk
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = talkCode,
                Title = parsedSection.Title,
                Description = $"Part of course: {request.Title}",
                Category = request.Category,
                SourceLanguageCode = request.SourceLanguageCode,
                Status = ToolboxTalkStatus.Published,
                IsActive = true
            };

            // Each talk gets the section content as a single section
            var section = new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = talk.Id,
                SectionNumber = 1,
                Title = parsedSection.Title,
                Content = parsedSection.Content,
                RequiresAcknowledgment = true
            };
            talk.Sections.Add(section);

            _dbContext.ToolboxTalks.Add(talk);
            courseTalks.Add(talk);

            // Sync quiz settings and questions to the course talk
            SyncQuizSettingsToTalk(talk, quizSettings);
            SyncQuizQuestionsToTalk(talk.Id, quizQuestions, session.InputMode);

            // Migrate translations from the draft talk to this course talk
            if (draftTranslations != null && draftSections != null)
            {
                // Find the matching draft section by index (sections are in the same order)
                var draftSection = orderIndex < draftSections.Count ? draftSections[orderIndex] : null;

                if (draftSection != null)
                {
                    foreach (var draftTranslation in draftTranslations)
                    {
                        // Extract the translated section data for this specific section
                        var translatedSectionJson = ExtractTranslatedSectionForId(
                            draftTranslation.TranslatedSections, draftSection.Id, section.Id);

                        if (translatedSectionJson != null)
                        {
                            // Extract the translated title for this section from the sections JSON
                            var translatedTitle = ExtractTranslatedSectionTitle(
                                draftTranslation.TranslatedSections, draftSection.Id)
                                ?? parsedSection.Title;

                            _dbContext.ToolboxTalkTranslations.Add(new ToolboxTalkTranslation
                            {
                                Id = Guid.NewGuid(),
                                TenantId = tenantId,
                                ToolboxTalkId = talk.Id,
                                LanguageCode = draftTranslation.LanguageCode,
                                TranslatedTitle = translatedTitle,
                                TranslatedDescription = $"Part of course: {request.Title}",
                                TranslatedSections = translatedSectionJson,
                                TranslatedQuestions = draftTranslation.TranslatedQuestions,
                                TranslatedAt = draftTranslation.TranslatedAt,
                                TranslationProvider = draftTranslation.TranslationProvider,
                                EmailSubject = translatedTitle,
                                EmailBody = translatedTitle
                            });
                        }
                    }
                }
            }

            var courseItem = new ToolboxTalkCourseItem
            {
                Id = Guid.NewGuid(),
                CourseId = course.Id,
                ToolboxTalkId = talk.Id,
                OrderIndex = orderIndex++,
                IsRequired = true
            };
            course.CourseItems.Add(courseItem);
        }

        // Create course-level translations from the draft talk translations
        if (draftTranslations != null)
        {
            foreach (var draftTranslation in draftTranslations)
            {
                _dbContext.ToolboxTalkCourseTranslations.Add(new ToolboxTalkCourseTranslation
                {
                    Id = Guid.NewGuid(),
                    CourseId = course.Id,
                    LanguageCode = draftTranslation.LanguageCode,
                    TranslatedTitle = draftTranslation.TranslatedTitle,
                    TranslatedDescription = draftTranslation.TranslatedDescription
                });
            }
        }

        _dbContext.ToolboxTalkCourses.Add(course);
        await SaveWithCodeRetryAsync(() => courseTalks.ToArray(), tenantId, cancellationToken);

        // Delete the orphaned draft talk now that translations have been migrated
        if (session.OutputTalkId.HasValue)
        {
            await DeleteDraftTalkAsync(session.OutputTalkId.Value, cancellationToken);
            session.OutputTalkId = null;
        }

        return course.Id;
    }

    /// <summary>
    /// Extracts a single section's translated data from the TranslatedSections JSON array,
    /// matching by the draft section's SectionId, and remaps to the new course talk's section ID.
    /// Returns a JSON array with a single element, or null if not found.
    /// Format: [{SectionId, Title, Content}]
    /// </summary>
    private static string? ExtractTranslatedSectionForId(string translatedSectionsJson, Guid draftSectionId, Guid newSectionId)
    {
        try
        {
            using var doc = JsonDocument.Parse(translatedSectionsJson);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("SectionId", out var sectionIdProp) &&
                    sectionIdProp.TryGetGuid(out var sectionId) &&
                    sectionId == draftSectionId)
                {
                    var title = element.TryGetProperty("Title", out var titleProp) ? titleProp.GetString() : null;
                    var content = element.TryGetProperty("Content", out var contentProp) ? contentProp.GetString() : null;

                    var remapped = new[] { new { SectionId = newSectionId, Title = title, Content = content } };
                    return JsonSerializer.Serialize(remapped);
                }
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — skip
        }

        return null;
    }

    /// <summary>
    /// Extracts the translated title for a specific section from the TranslatedSections JSON.
    /// </summary>
    private static string? ExtractTranslatedSectionTitle(string translatedSectionsJson, Guid draftSectionId)
    {
        try
        {
            using var doc = JsonDocument.Parse(translatedSectionsJson);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("SectionId", out var sectionIdProp) &&
                    sectionIdProp.TryGetGuid(out var sectionId) &&
                    sectionId == draftSectionId)
                {
                    return element.TryGetProperty("Title", out var titleProp) ? titleProp.GetString() : null;
                }
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — skip
        }

        return null;
    }

    /// <summary>
    /// Soft-deletes the orphaned draft talk and all its related data (sections, questions, translations).
    /// </summary>
    private async Task DeleteDraftTalkAsync(Guid draftTalkId, CancellationToken cancellationToken)
    {
        var draftTalk = await _dbContext.ToolboxTalks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == draftTalkId && !t.IsDeleted, cancellationToken);

        if (draftTalk == null) return;

        draftTalk.IsDeleted = true;

        var draftSections = await _dbContext.ToolboxTalkSections
            .IgnoreQueryFilters()
            .Where(s => s.ToolboxTalkId == draftTalkId && !s.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var s in draftSections) s.IsDeleted = true;

        var draftQuestions = await _dbContext.ToolboxTalkQuestions
            .IgnoreQueryFilters()
            .Where(q => q.ToolboxTalkId == draftTalkId)
            .ToListAsync(cancellationToken);
        foreach (var q in draftQuestions) _dbContext.ToolboxTalkQuestions.Remove(q);

        var draftTranslations = await _dbContext.ToolboxTalkTranslations
            .IgnoreQueryFilters()
            .Where(t => t.ToolboxTalkId == draftTalkId && !t.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var t in draftTranslations) t.IsDeleted = true;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[ContentCreationSession] Deleted orphaned draft talk {DraftTalkId} after course publish",
            draftTalkId);
    }

    private async Task<string> GenerateCodeAsync(string? title, Guid tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title))
            title = "TALK";

        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !CommonWords.Contains(w))
            .ToList();

        string prefix;
        if (words.Count >= 2)
        {
            prefix = string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
        }
        else
        {
            var cleaned = new string(title.Where(c => !char.IsWhiteSpace(c)).ToArray());
            prefix = cleaned.Length >= 3
                ? cleaned[..3].ToUpperInvariant()
                : cleaned.ToUpperInvariant();
        }

        if (prefix.Length > 16)
            prefix = prefix[..16];

        var pattern = prefix + "-";
        var existingCodes = await _dbContext.ToolboxTalks
            .Where(t => t.TenantId == tenantId && t.Code.StartsWith(pattern))
            .Select(t => t.Code)
            .ToListAsync(cancellationToken);

        var maxNumber = 0;
        foreach (var existingCode in existingCodes)
        {
            var suffix = existingCode[pattern.Length..];
            if (int.TryParse(suffix, out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"{prefix}-{(maxNumber + 1):D3}";
    }

    private static bool IsUniqueCodeViolation(DbUpdateException ex)
    {
        return ex.InnerException is PostgresException pgEx
            && pgEx.SqlState == PostgresErrorCodes.UniqueViolation
            && pgEx.ConstraintName?.Contains("Code", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task SaveWithCodeRetryAsync(
        Func<ToolboxTalk[]> getTalks,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException ex) when (attempt < maxAttempts && IsUniqueCodeViolation(ex))
            {
                _logger.LogWarning(
                    "Talk code unique constraint violation on attempt {Attempt}, regenerating codes",
                    attempt);

                foreach (var talk in getTalks())
                {
                    talk.Code = await GenerateCodeAsync(talk.Title, tenantId, cancellationToken);
                }
            }
        }
    }

    private static ContentCreationSessionDto MapToDto(ContentCreationSession session)
    {
        return new ContentCreationSessionDto
        {
            Id = session.Id,
            InputMode = session.InputMode,
            Status = session.Status,
            SourceText = session.SourceText,
            SourceFileName = session.SourceFileName,
            SourceFileUrl = session.SourceFileUrl,
            SourceFileType = session.SourceFileType,
            TranscriptText = session.TranscriptText,
            ParsedSectionsJson = session.ParsedSectionsJson,
            OutputType = session.OutputType,
            OutputTalkId = session.OutputTalkId,
            OutputCourseId = session.OutputCourseId,
            TargetLanguageCodes = session.TargetLanguageCodes,
            PassThreshold = session.PassThreshold,
            SectorKey = session.SectorKey,
            ReviewerName = session.ReviewerName,
            ReviewerOrg = session.ReviewerOrg,
            ReviewerRole = session.ReviewerRole,
            DocumentRef = session.DocumentRef,
            ClientName = session.ClientName,
            AuditPurpose = session.AuditPurpose,
            ExpiresAt = session.ExpiresAt,
            ValidationRunIds = session.ValidationRunIds,
            QuestionsJson = session.QuestionsJson,
            QuizSettingsJson = session.QuizSettingsJson,
            SettingsJson = session.SettingsJson,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt
        };
    }

    /// <summary>
    /// Syncs quiz settings from the session onto the draft ToolboxTalk entity.
    /// </summary>
    private static void SyncQuizSettingsToTalk(ToolboxTalk talk, SessionQuizSettingsDto? quizSettings)
    {
        if (quizSettings == null) return;

        talk.RequiresQuiz = quizSettings.RequireQuiz;
        talk.PassingScore = quizSettings.PassingScore;
        talk.ShuffleQuestions = quizSettings.ShuffleQuestions;
        talk.ShuffleOptions = quizSettings.ShuffleOptions;
    }

    /// <summary>
    /// Syncs quiz questions from session JSON to the draft ToolboxTalk (via DbContext direct add).
    /// Used when the talk already exists (re-run scenario).
    /// </summary>
    private void SyncQuizQuestionsToTalk(Guid talkId, List<SessionQuizQuestionDto>? quizQuestions, InputMode inputMode = InputMode.Text)
    {
        if (quizQuestions == null || quizQuestions.Count == 0) return;

        var source = inputMode switch
        {
            InputMode.Video => ContentSource.Video,
            InputMode.Pdf => ContentSource.Pdf,
            _ => ContentSource.Manual
        };

        var questionNumber = 1;
        foreach (var q in quizQuestions)
        {
            var options = q.Options;
            var correctAnswer = q.CorrectAnswerIndex >= 0 && q.CorrectAnswerIndex < options.Count
                ? options[q.CorrectAnswerIndex]
                : string.Empty;

            _dbContext.ToolboxTalkQuestions.Add(new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = talkId,
                QuestionNumber = questionNumber++,
                QuestionText = q.QuestionText,
                QuestionType = Enum.TryParse<QuestionType>(q.QuestionType, out var qt) ? qt : QuestionType.MultipleChoice,
                Options = JsonSerializer.Serialize(options),
                CorrectAnswer = correctAnswer,
                CorrectOptionIndex = q.CorrectAnswerIndex,
                Points = q.Points,
                Source = source
            });
        }
    }

    #endregion
}
