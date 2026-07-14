using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.AddTargetLanguage;

public class AddTargetLanguageCommandHandler
    : IRequestHandler<AddTargetLanguageCommand, Result<ToolboxTalkDto>>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly ILanguageCodeService _languageCodeService;
    private readonly ILogger<AddTargetLanguageCommandHandler> _logger;

    public AddTargetLanguageCommandHandler(
        IToolboxTalksDbContext context,
        ILanguageCodeService languageCodeService,
        ILogger<AddTargetLanguageCommandHandler> logger)
    {
        _context = context;
        _languageCodeService = languageCodeService;
        _logger = logger;
    }

    public async Task<Result<ToolboxTalkDto>> Handle(
        AddTargetLanguageCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.LanguageCode))
            return Result.Fail<ToolboxTalkDto>("Language code is required.");

        var talk = await _context.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == request.ToolboxTalkId
                                   && t.TenantId == request.TenantId
                                   && !t.IsDeleted,
                                  cancellationToken);

        if (talk is null)
            return Result.Fail<ToolboxTalkDto>("Learning not found.");

        // Validate language code against the supported language list.
        // GetAllLanguagesAsync returns a name→code dictionary; we check the values.
        // If the lookup returns empty (configuration gap), skip validation rather than blocking.
        var allLanguages = await _languageCodeService.GetAllLanguagesAsync();
        if (allLanguages.Count > 0
            && !allLanguages.Values.Contains(request.LanguageCode, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Fail<ToolboxTalkDto>(
                $"Language code '{request.LanguageCode}' is not in the supported language list.");
        }

        // Deserialise existing codes; treat null/empty as an empty list
        List<string> existing;
        if (string.IsNullOrWhiteSpace(talk.TargetLanguageCodes))
        {
            existing = new List<string>();
        }
        else
        {
            try
            {
                existing = JsonSerializer.Deserialize<List<string>>(talk.TargetLanguageCodes)
                           ?? new List<string>();
            }
            catch (JsonException)
            {
                existing = new List<string>();
            }
        }

        // Reject duplicates
        if (existing.Any(c => string.Equals(c, request.LanguageCode, StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Fail<ToolboxTalkDto>(
                $"Language '{request.LanguageCode}' is already a target language for this learning.");
        }

        existing.Add(request.LanguageCode.ToLowerInvariant());
        talk.TargetLanguageCodes = JsonSerializer.Serialize(existing);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "AddTargetLanguage: talk {TalkId} — added '{Code}', target languages now {Codes}",
            talk.Id, request.LanguageCode, talk.TargetLanguageCodes);

        return Result.Ok(MapToDto(talk));
    }

    private static ToolboxTalkDto MapToDto(ToolboxTalk talk) => new()
    {
        Id = talk.Id,
        Code = talk.Code,
        Title = talk.Title,
        Description = talk.Description,
        Category = talk.Category,
        Frequency = talk.Frequency,
        FrequencyDisplay = talk.Frequency switch
        {
            ToolboxTalkFrequency.Once => "One-time",
            ToolboxTalkFrequency.Weekly => "Weekly",
            ToolboxTalkFrequency.Monthly => "Monthly",
            ToolboxTalkFrequency.Annually => "Annually",
            _ => talk.Frequency.ToString()
        },
        VideoUrl = talk.VideoUrl,
        VideoSource = talk.VideoSource,
        VideoSourceDisplay = talk.VideoSource.ToString(),
        AttachmentUrl = talk.AttachmentUrl,
        MinimumVideoWatchPercent = talk.MinimumVideoWatchPercent,
        RequiresQuiz = talk.RequiresQuiz,
        PassingScore = talk.PassingScore,
        IsActive = talk.IsActive,
        Status = talk.Status,
        StatusDisplay = talk.Status switch
        {
            ToolboxTalkStatus.Draft => "Draft",
            ToolboxTalkStatus.Processing => "Processing",
            ToolboxTalkStatus.ReadyForReview => "Ready for Review",
            ToolboxTalkStatus.Published => "Published",
            _ => talk.Status.ToString()
        },
        PdfUrl = talk.PdfUrl,
        PdfFileName = talk.PdfFileName,
        GeneratedFromVideo = talk.GeneratedFromVideo,
        GeneratedFromPdf = talk.GeneratedFromPdf,
        GenerateSlidesFromPdf = talk.GenerateSlidesFromPdf,
        SlidesGenerated = talk.SlidesGenerated,
        SlideCount = 0,
        SlideshowHtml = talk.SlideshowHtml,
        SlideshowGeneratedAt = talk.SlideshowGeneratedAt,
        QuizQuestionCount = talk.QuizQuestionCount,
        ShuffleQuestions = talk.ShuffleQuestions,
        ShuffleOptions = talk.ShuffleOptions,
        UseQuestionPool = talk.UseQuestionPool,
        AllowRetry = talk.AllowRetry,
        IsPartOfCourse = talk.IsPartOfCourse,
        AutoAssignToNewEmployees = talk.AutoAssignToNewEmployees,
        AutoAssignDueDays = talk.AutoAssignDueDays,
        SourceLanguageCode = talk.SourceLanguageCode,
        GenerateCertificate = talk.GenerateCertificate,
        RequiresRefresher = talk.RequiresRefresher,
        RefresherIntervalMonths = talk.RefresherIntervalMonths,
        LastEditedStep = talk.LastEditedStep,
        SourceFileUrl = talk.SourceFileUrl,
        SourceFileName = talk.SourceFileName,
        SourceFileType = talk.SourceFileType,
        SourceText = talk.SourceText,
        TargetLanguageCodes = talk.TargetLanguageCodes,
        ReviewerName = talk.ReviewerName,
        ReviewerOrg = talk.ReviewerOrg,
        ReviewerRole = talk.ReviewerRole,
        DocumentRef = talk.DocumentRef,
        ClientName = talk.ClientName,
        AuditPurpose = talk.AuditPurpose,
        AudienceRole = talk.AudienceRole,
        PreserveSourceWording = talk.PreserveSourceWording,
        InputMode = talk.InputMode,
        PublishedAt = talk.PublishedAt,
        CreatedAt = talk.CreatedAt,
        UpdatedAt = talk.UpdatedAt,
        Sections = [],
        Questions = [],
        Translations = [],
    };
}
