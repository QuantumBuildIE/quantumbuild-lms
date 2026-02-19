using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkPreview;

public class GetToolboxTalkPreviewQueryHandler
    : IRequestHandler<GetToolboxTalkPreviewQuery, ToolboxTalkPreviewDto?>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly ILanguageCodeService _languageCodeService;

    public GetToolboxTalkPreviewQueryHandler(
        IToolboxTalksDbContext context,
        ILanguageCodeService languageCodeService)
    {
        _context = context;
        _languageCodeService = languageCodeService;
    }

    public async Task<ToolboxTalkPreviewDto?> Handle(
        GetToolboxTalkPreviewQuery request,
        CancellationToken cancellationToken)
    {
        var talk = await _context.ToolboxTalks
            .Include(t => t.Sections.OrderBy(s => s.SectionNumber))
            .Include(t => t.Questions.OrderBy(q => q.QuestionNumber))
            .Include(t => t.Translations)
            .Include(t => t.Slides.Where(s => !s.IsDeleted))
            .Include(t => t.SlideshowTranslations)
            .Where(t => t.Id == request.ToolboxTalkId
                && t.TenantId == request.TenantId
                && !t.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (talk == null)
            return null;

        var languageCode = request.LanguageCode ?? talk.SourceLanguageCode;

        // Get translation if the requested language differs from source
        var translation = talk.Translations?
            .FirstOrDefault(t => t.LanguageCode == languageCode);

        var translatedSections = ParseTranslatedSections(translation?.TranslatedSections);
        var translatedQuestions = ParseTranslatedQuestions(translation?.TranslatedQuestions);

        // Build sections with translated content
        var sections = talk.Sections.Select(section =>
        {
            var translated = translatedSections?
                .FirstOrDefault(ts => ts.SectionId == section.Id);

            return new PreviewSectionDto
            {
                Id = section.Id,
                SectionNumber = section.SectionNumber,
                Title = translated?.Title ?? section.Title,
                Content = translated?.Content ?? section.Content,
                RequiresAcknowledgment = section.RequiresAcknowledgment
            };
        }).ToList();

        // Build questions without correct answers (employee view)
        var questions = talk.Questions.Select(question =>
        {
            var translated = translatedQuestions?
                .FirstOrDefault(tq => tq.QuestionId == question.Id);

            return new PreviewQuestionDto
            {
                Id = question.Id,
                QuestionNumber = question.QuestionNumber,
                QuestionText = translated?.QuestionText ?? question.QuestionText,
                QuestionType = question.QuestionType,
                QuestionTypeDisplay = GetQuestionTypeDisplay(question.QuestionType),
                Options = translated?.Options ?? ParseOptions(question.Options),
                Points = question.Points
            };
        }).ToList();

        // Build available translations list
        var availableTranslations = new List<ToolboxTalkTranslationDto>();
        foreach (var t in talk.Translations ?? Enumerable.Empty<Domain.Entities.ToolboxTalkTranslation>())
        {
            availableTranslations.Add(new ToolboxTalkTranslationDto
            {
                LanguageCode = t.LanguageCode,
                Language = await _languageCodeService.GetLanguageNameAsync(t.LanguageCode),
                TranslatedTitle = t.TranslatedTitle,
                TranslatedAt = t.TranslatedAt,
                TranslationProvider = t.TranslationProvider
            });
        }
        availableTranslations = availableTranslations.OrderBy(t => t.Language).ToList();

        // Get translated slideshow HTML if available
        var slideshowHtml = talk.SlideshowHtml;
        var slideshowGeneratedAt = talk.SlideshowGeneratedAt;
        if (!string.IsNullOrEmpty(languageCode) &&
            !string.Equals(languageCode, talk.SourceLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            var slideshowTranslation = talk.SlideshowTranslations?
                .FirstOrDefault(st => st.LanguageCode == languageCode);
            if (slideshowTranslation != null)
            {
                slideshowHtml = slideshowTranslation.TranslatedHtml;
                slideshowGeneratedAt = slideshowTranslation.TranslatedAt;
            }
        }

        return new ToolboxTalkPreviewDto
        {
            Id = talk.Id,
            Title = translation?.TranslatedTitle ?? talk.Title,
            Description = translation?.TranslatedDescription ?? talk.Description,
            Category = talk.Category,
            VideoUrl = talk.VideoUrl,
            VideoSource = talk.VideoSource,
            RequiresQuiz = talk.RequiresQuiz,
            PassingScore = talk.PassingScore,
            SlidesGenerated = talk.SlidesGenerated,
            SlideCount = talk.Slides.Count,
            SlideshowHtml = slideshowHtml,
            SlideshowGeneratedAt = slideshowGeneratedAt,
            SourceLanguageCode = talk.SourceLanguageCode,
            PreviewLanguageCode = languageCode,
            AvailableTranslations = availableTranslations,
            Sections = sections,
            Questions = questions
        };
    }

    private static List<TranslatedSectionData>? ParseTranslatedSections(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<TranslatedSectionData>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static List<TranslatedQuestionData>? ParseTranslatedQuestions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<TranslatedQuestionData>>(json);
        }
        catch
        {
            return null;
        }
    }

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

    private static string GetQuestionTypeDisplay(QuestionType type) => type switch
    {
        QuestionType.MultipleChoice => "Multiple Choice",
        QuestionType.TrueFalse => "True/False",
        QuestionType.ShortAnswer => "Short Answer",
        _ => type.ToString()
    };

    private class TranslatedSectionData
    {
        public Guid SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class TranslatedQuestionData
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public List<string>? Options { get; set; }
    }
}
