using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkById;

public class GetToolboxTalkByIdQueryHandler : IRequestHandler<GetToolboxTalkByIdQuery, ToolboxTalkDto?>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly ILanguageCodeService _languageCodeService;

    public GetToolboxTalkByIdQueryHandler(
        IToolboxTalksDbContext context,
        ILanguageCodeService languageCodeService)
    {
        _context = context;
        _languageCodeService = languageCodeService;
    }

    public async Task<ToolboxTalkDto?> Handle(GetToolboxTalkByIdQuery request, CancellationToken cancellationToken)
    {
        var talk = await _context.ToolboxTalks
            .Include(t => t.Sections.OrderBy(s => s.SectionNumber))
            .Include(t => t.Questions.OrderBy(q => q.QuestionNumber))
            .Include(t => t.Translations)
            .Include(t => t.Slides.Where(s => !s.IsDeleted))
            .Where(t => t.Id == request.Id && t.TenantId == request.TenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (talk == null)
            return null;

        // Get completion statistics
        var now = DateTime.UtcNow;
        var stats = await _context.ScheduledTalks
            .Where(st => st.ToolboxTalkId == talk.Id && st.TenantId == request.TenantId && !st.IsDeleted)
            .GroupBy(st => st.ToolboxTalkId)
            .Select(g => new
            {
                TotalAssignments = g.Count(),
                CompletedCount = g.Count(st => st.Status == ScheduledTalkStatus.Completed),
                OverdueCount = g.Count(st => st.Status == ScheduledTalkStatus.Overdue ||
                    (st.Status != ScheduledTalkStatus.Completed && st.Status != ScheduledTalkStatus.Cancelled && st.DueDate < now)),
                PendingCount = g.Count(st => st.Status == ScheduledTalkStatus.Pending),
                InProgressCount = g.Count(st => st.Status == ScheduledTalkStatus.InProgress)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new ToolboxTalkDto
        {
            Id = talk.Id,
            Title = talk.Title,
            Description = talk.Description,
            Category = talk.Category,
            Frequency = talk.Frequency,
            FrequencyDisplay = GetFrequencyDisplay(talk.Frequency),
            VideoUrl = talk.VideoUrl,
            VideoSource = talk.VideoSource,
            VideoSourceDisplay = GetVideoSourceDisplay(talk.VideoSource),
            AttachmentUrl = talk.AttachmentUrl,
            MinimumVideoWatchPercent = talk.MinimumVideoWatchPercent,
            RequiresQuiz = talk.RequiresQuiz,
            PassingScore = talk.PassingScore,
            IsActive = talk.IsActive,
            Status = talk.Status,
            StatusDisplay = GetStatusDisplay(talk.Status),
            PdfUrl = talk.PdfUrl,
            PdfFileName = talk.PdfFileName,
            GeneratedFromVideo = talk.GeneratedFromVideo,
            GeneratedFromPdf = talk.GeneratedFromPdf,
            GenerateSlidesFromPdf = talk.GenerateSlidesFromPdf,
            SlidesGenerated = talk.SlidesGenerated,
            SlideCount = talk.Slides.Count,
            SlideshowHtml = talk.SlideshowHtml,
            SlideshowGeneratedAt = talk.SlideshowGeneratedAt,
            QuizQuestionCount = talk.QuizQuestionCount,
            ShuffleQuestions = talk.ShuffleQuestions,
            ShuffleOptions = talk.ShuffleOptions,
            UseQuestionPool = talk.UseQuestionPool,
            AutoAssignToNewEmployees = talk.AutoAssignToNewEmployees,
            AutoAssignDueDays = talk.AutoAssignDueDays,
            SourceLanguageCode = talk.SourceLanguageCode,
            GenerateCertificate = talk.GenerateCertificate,
            RequiresRefresher = talk.RequiresRefresher,
            RefresherIntervalMonths = talk.RefresherIntervalMonths,
            Sections = talk.Sections.Select(s => new ToolboxTalkSectionDto
            {
                Id = s.Id,
                ToolboxTalkId = s.ToolboxTalkId,
                SectionNumber = s.SectionNumber,
                Title = s.Title,
                Content = s.Content,
                RequiresAcknowledgment = s.RequiresAcknowledgment,
                Source = s.Source,
                SourceDisplay = GetContentSourceDisplay(s.Source),
                VideoTimestamp = s.VideoTimestamp
            }).ToList(),
            Questions = talk.Questions.Select(q => new ToolboxTalkQuestionDto
            {
                Id = q.Id,
                ToolboxTalkId = q.ToolboxTalkId,
                QuestionNumber = q.QuestionNumber,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                QuestionTypeDisplay = GetQuestionTypeDisplay(q.QuestionType),
                Options = ParseOptions(q.Options),
                CorrectAnswer = q.CorrectAnswer,
                Points = q.Points,
                Source = q.Source,
                SourceDisplay = GetContentSourceDisplay(q.Source),
                VideoTimestamp = q.VideoTimestamp,
                IsFromVideoFinalPortion = q.IsFromVideoFinalPortion
            }).ToList(),
            Translations = talk.Translations.Select(t => new ToolboxTalkTranslationDto
            {
                LanguageCode = t.LanguageCode,
                Language = _languageCodeService.GetLanguageName(t.LanguageCode),
                TranslatedTitle = t.TranslatedTitle,
                TranslatedAt = t.TranslatedAt,
                TranslationProvider = t.TranslationProvider
            }).OrderBy(t => t.Language).ToList(),
            CompletionStats = stats != null ? new ToolboxTalkCompletionStatsDto
            {
                TotalAssignments = stats.TotalAssignments,
                CompletedCount = stats.CompletedCount,
                OverdueCount = stats.OverdueCount,
                PendingCount = stats.PendingCount,
                InProgressCount = stats.InProgressCount,
                CompletionRate = stats.TotalAssignments > 0
                    ? Math.Round((decimal)stats.CompletedCount / stats.TotalAssignments * 100, 2)
                    : 0
            } : null,
            CreatedAt = talk.CreatedAt,
            UpdatedAt = talk.UpdatedAt
        };
    }

    private static string GetFrequencyDisplay(ToolboxTalkFrequency frequency) => frequency switch
    {
        ToolboxTalkFrequency.Once => "One-time",
        ToolboxTalkFrequency.Weekly => "Weekly",
        ToolboxTalkFrequency.Monthly => "Monthly",
        ToolboxTalkFrequency.Annually => "Annually",
        _ => frequency.ToString()
    };

    private static string GetVideoSourceDisplay(VideoSource source) => source switch
    {
        VideoSource.None => "None",
        VideoSource.YouTube => "YouTube",
        VideoSource.GoogleDrive => "Google Drive",
        VideoSource.Vimeo => "Vimeo",
        VideoSource.DirectUrl => "Direct URL",
        _ => source.ToString()
    };

    private static string GetStatusDisplay(ToolboxTalkStatus status) => status switch
    {
        ToolboxTalkStatus.Draft => "Draft",
        ToolboxTalkStatus.Processing => "Processing",
        ToolboxTalkStatus.ReadyForReview => "Ready for Review",
        ToolboxTalkStatus.Published => "Published",
        _ => status.ToString()
    };

    private static string GetQuestionTypeDisplay(QuestionType type) => type switch
    {
        QuestionType.MultipleChoice => "Multiple Choice",
        QuestionType.TrueFalse => "True/False",
        QuestionType.ShortAnswer => "Short Answer",
        _ => type.ToString()
    };

    private static string GetContentSourceDisplay(ContentSource source) => source switch
    {
        ContentSource.Manual => "Manual",
        ContentSource.Video => "Video",
        ContentSource.Pdf => "PDF",
        ContentSource.Both => "Video & PDF",
        _ => source.ToString()
    };

    private static List<string>? ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(optionsJson);
        }
        catch
        {
            return null;
        }
    }
}
