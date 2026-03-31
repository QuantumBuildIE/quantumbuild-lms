using Microsoft.AspNetCore.Http;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;

/// <summary>
/// Manages the lifecycle of a content creation wizard session
/// </summary>
public interface IContentCreationSessionService
{
    Task<ContentCreationSessionDto> CreateSessionAsync(
        CreateSessionRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> UploadFileAsync(
        Guid sessionId,
        IFormFile file,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> UpdateSourceAsync(
        Guid sessionId,
        UpdateSourceRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> ParseContentAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> UpdateSectionsAsync(
        Guid sessionId,
        UpdateSectionsRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> StartTranslateValidateAsync(
        Guid sessionId,
        StartTranslateValidateRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> GetSessionAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task AbandonSessionAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<PublishResult> PublishAsync(
        Guid sessionId,
        PublishRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> GenerateQuizAsync(
        Guid sessionId,
        Guid tenantId,
        int minimumQuestionsPerSection = 2,
        CancellationToken cancellationToken = default);

    Task<SessionQuizDataDto> GetQuizDataAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> UpdateQuestionsAsync(
        Guid sessionId,
        UpdateSessionQuestionsRequest request,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> UpdateQuizSettingsAsync(
        Guid sessionId,
        SessionQuizSettingsDto settings,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<SessionSettingsDto> GetSettingsAsync(
        Guid sessionId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> UpdateSettingsAsync(
        Guid sessionId,
        SessionSettingsDto settings,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<ContentCreationSessionDto> UploadCoverImageAsync(
        Guid sessionId,
        IFormFile file,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}

#region DTOs

public record CreateSessionRequest
{
    public InputMode InputMode { get; init; }
    public string? SourceText { get; init; }
    public string? SectorKey { get; init; }
    public int PassThreshold { get; init; } = 75;
    public bool IncludeQuiz { get; init; } = true;

    // Audit metadata
    public string? ReviewerName { get; init; }
    public string? ReviewerOrg { get; init; }
    public string? ReviewerRole { get; init; }
    public string? DocumentRef { get; init; }
    public string? ClientName { get; init; }
    public string? AuditPurpose { get; init; }
}

public record UpdateSourceRequest
{
    public string? SourceText { get; init; }
}

public record UpdateSectionsRequest
{
    public List<UpdatedSection> Sections { get; init; } = new();
    public OutputType OutputType { get; init; }
}

public record UpdatedSection
{
    public string Title { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int Order { get; init; }
}

public record StartTranslateValidateRequest
{
    public List<string> TargetLanguageCodes { get; init; } = new();
}

public record PublishRequest
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public string? Code { get; init; }
    public string SourceLanguageCode { get; init; } = "en";
}

public record ContentCreationSessionDto
{
    public Guid Id { get; init; }
    public InputMode InputMode { get; init; }
    public ContentCreationSessionStatus Status { get; init; }
    public string? SourceText { get; init; }
    public string? SourceFileName { get; init; }
    public string? SourceFileUrl { get; init; }
    public string? SourceFileType { get; init; }
    public string? TranscriptText { get; init; }
    public string? ParsedSectionsJson { get; init; }
    public OutputType? OutputType { get; init; }
    public Guid? OutputTalkId { get; init; }
    public Guid? OutputCourseId { get; init; }
    public string? TargetLanguageCodes { get; init; }
    public int PassThreshold { get; init; }
    public bool IncludeQuiz { get; init; }
    public string? SectorKey { get; init; }
    public string? ReviewerName { get; init; }
    public string? ReviewerOrg { get; init; }
    public string? ReviewerRole { get; init; }
    public string? DocumentRef { get; init; }
    public string? ClientName { get; init; }
    public string? AuditPurpose { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string? ValidationRunIds { get; init; }
    public string? QuestionsJson { get; init; }
    public string? QuizSettingsJson { get; init; }
    public string? SettingsJson { get; init; }
    public string? SubtitleJobId { get; init; }
    public string? TranscriptWordsJson { get; init; }
    public string? TranscriptionJobId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record SessionQuizQuestionDto
{
    public string Id { get; init; } = string.Empty;
    public int SectionIndex { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public string QuestionType { get; init; } = "MultipleChoice"; // MultipleChoice, TrueFalse, ShortAnswer
    public List<string> Options { get; init; } = new();
    public int CorrectAnswerIndex { get; init; }
    public int Points { get; init; } = 1;
    public bool IsAiGenerated { get; init; }
}

public record SessionQuizSettingsDto
{
    public bool RequireQuiz { get; init; } = true;
    public int PassingScore { get; init; } = 80;
    public bool ShuffleQuestions { get; init; }
    public bool ShuffleOptions { get; init; }
    public bool AllowRetry { get; init; } = true;
}

public record SessionQuizDataDto
{
    public List<SessionQuizQuestionDto> Questions { get; init; } = new();
    public SessionQuizSettingsDto Settings { get; init; } = new();
}

public record UpdateSessionQuestionsRequest
{
    public List<SessionQuizQuestionDto> Questions { get; init; } = new();
}

public record PublishResult(
    bool Success,
    Guid? OutputId,
    OutputType? OutputType,
    string? ErrorMessage = null);

public record SessionSettingsDto
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? CoverImageUrl { get; init; }
    public string? Category { get; init; }
    public string RefresherFrequency { get; init; } = "Once"; // Once, Monthly, Quarterly, Annually
    public bool IsActiveOnPublish { get; init; } = true;
    public bool GenerateCertificate { get; init; } = true;
    public int MinimumWatchPercent { get; init; } = 90;
    public bool AutoAssign { get; init; }
    public int AutoAssignDueDays { get; init; } = 14;
    public bool GenerateSlideshow { get; init; }
    public string SlideshowSource { get; init; } = "none";
}

#endregion
