using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
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
        IOptions<TranslationValidationSettings> validationSettings,
        ILogger<ContentCreationSessionService> logger)
    {
        _dbContext = dbContext;
        _parserService = parserService;
        _storageService = storageService;
        _aiQuizService = aiQuizService;
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

    public async Task<ContentCreationSessionDto> ParseContentAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionEntityAsync(sessionId, tenantId, cancellationToken);

        if (session.Status != ContentCreationSessionStatus.Draft)
            throw new InvalidOperationException("Content can only be parsed in Draft status");

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
            session.Status != ContentCreationSessionStatus.Validated)
            throw new InvalidOperationException("Translation/validation can only start from Parsed or Validated status");

        if (request.TargetLanguageCodes.Count == 0)
            throw new InvalidOperationException("At least one target language code is required");

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
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Enqueue Hangfire jobs after save
        foreach (var runId in runIds)
        {
            BackgroundJob.Enqueue<TranslationValidationJob>(
                job => job.ExecuteAsync(runId, tenantId, CancellationToken.None));
        }

        _logger.LogInformation(
            "[ContentCreationSession] Started translate+validate for session {SessionId}: {Count} languages",
            sessionId, request.TargetLanguageCodes.Count);

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

        session.Status = ContentCreationSessionStatus.Publishing;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var sections = JsonSerializer.Deserialize<List<ParsedSection>>(session.ParsedSectionsJson)
                ?? new List<ParsedSection>();

            if (session.OutputType == OutputType.Lesson)
            {
                var outputId = await PublishAsLessonAsync(
                    session, sections, request, tenantId, cancellationToken);
                session.OutputId = outputId;
            }
            else
            {
                var outputId = await PublishAsCourseAsync(
                    session, sections, request, tenantId, cancellationToken);
                session.OutputId = outputId;
            }

            session.Status = ContentCreationSessionStatus.Completed;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "[ContentCreationSession] Published session {SessionId} as {OutputType} {OutputId}",
                sessionId, session.OutputType, session.OutputId);

            return new PublishResult(true, session.OutputId, session.OutputType);
        }
        catch (Exception ex)
        {
            session.Status = ContentCreationSessionStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "[ContentCreationSession] Publish failed for session {SessionId}", sessionId);
            return new PublishResult(false, null, null, ex.Message);
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

        var sections = JsonSerializer.Deserialize<List<ParsedSection>>(session.ParsedSectionsJson) ?? new();
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
            ? JsonSerializer.Deserialize<List<SessionQuizQuestionDto>>(session.QuestionsJson) ?? new()
            : new List<SessionQuizQuestionDto>();

        var settings = !string.IsNullOrWhiteSpace(session.QuizSettingsJson)
            ? JsonSerializer.Deserialize<SessionQuizSettingsDto>(session.QuizSettingsJson) ?? new()
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
            return JsonSerializer.Deserialize<SessionSettingsDto>(session.SettingsJson) ?? new();

        // Build defaults from parsed content
        var suggestedTitle = string.Empty;
        if (!string.IsNullOrWhiteSpace(session.ParsedSectionsJson))
        {
            try
            {
                var sections = JsonSerializer.Deserialize<List<ParsedSection>>(session.ParsedSectionsJson);
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
            ? JsonSerializer.Deserialize<SessionSettingsDto>(session.SettingsJson) ?? new()
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
        // Validate title uniqueness (same pattern as CreateToolboxTalkCommandHandler)
        var titleExists = await _dbContext.ToolboxTalks
            .AnyAsync(t => t.TenantId == tenantId && t.Title == request.Title, cancellationToken);

        if (titleExists)
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

        var talk = new ToolboxTalk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = code,
            Title = request.Title,
            Description = request.Description,
            Category = request.Category,
            SourceLanguageCode = request.SourceLanguageCode,
            Status = ToolboxTalkStatus.Draft,
            VideoUrl = session.InputMode == InputMode.Video ? session.SourceFileUrl : null,
            VideoSource = session.InputMode == InputMode.Video ? VideoSource.DirectUrl : VideoSource.None,
            PdfUrl = session.InputMode == InputMode.Pdf ? session.SourceFileUrl : null,
            PdfFileName = session.InputMode == InputMode.Pdf ? session.SourceFileName : null,
            IsActive = true
        };

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

        _dbContext.ToolboxTalks.Add(talk);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return talk.Id;
    }

    private async Task<Guid> PublishAsCourseAsync(
        ContentCreationSession session,
        List<ParsedSection> sections,
        PublishRequest request,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Validate title uniqueness (same pattern as CreateToolboxTalkCourseCommandHandler)
        var titleExists = await _dbContext.ToolboxTalkCourses
            .AnyAsync(c => c.TenantId == tenantId && c.Title == request.Title && !c.IsDeleted, cancellationToken);

        if (titleExists)
            throw new InvalidOperationException($"A course with title '{request.Title}' already exists.");

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
                Status = ToolboxTalkStatus.Draft,
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

        _dbContext.ToolboxTalkCourses.Add(course);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return course.Id;
    }

    private async Task<string> GenerateCodeAsync(string title, Guid tenantId, CancellationToken cancellationToken)
    {
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
            OutputId = session.OutputId,
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

    #endregion
}
