using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetMyToolboxTalkById;

public class GetMyToolboxTalkByIdQueryHandler : IRequestHandler<GetMyToolboxTalkByIdQuery, MyToolboxTalkDto?>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly IQuizGenerationService _quizGenerationService;

    public GetMyToolboxTalkByIdQueryHandler(
        IToolboxTalksDbContext context,
        IQuizGenerationService quizGenerationService)
    {
        _context = context;
        _quizGenerationService = quizGenerationService;
    }

    public async Task<MyToolboxTalkDto?> Handle(GetMyToolboxTalkByIdQuery request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Get the scheduled talk with all related data
        var scheduledTalk = await _context.ScheduledTalks
            .Include(st => st.ToolboxTalk)
                .ThenInclude(t => t.Sections.OrderBy(s => s.SectionNumber))
            .Include(st => st.ToolboxTalk)
                .ThenInclude(t => t.Questions.OrderBy(q => q.QuestionNumber))
            .Include(st => st.ToolboxTalk)
                .ThenInclude(t => t.Translations)
            .Include(st => st.SectionProgress)
            .Include(st => st.QuizAttempts.OrderByDescending(qa => qa.AttemptedAt))
            .Include(st => st.Completion)
            .Include(st => st.Employee)
            .Where(st => st.Id == request.ScheduledTalkId &&
                        st.TenantId == request.TenantId &&
                        st.EmployeeId == request.EmployeeId &&
                        !st.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (scheduledTalk == null)
            return null;

        var talk = scheduledTalk.ToolboxTalk;
        // Always use the employee's current preferred language, not the frozen ScheduledTalk.LanguageCode
        var languageCode = scheduledTalk.Employee?.PreferredLanguage ?? "en";

        // Get translation if available and not English
        var translation = talk.Translations?
            .FirstOrDefault(t => t.LanguageCode == languageCode);

        // Parse translated sections if available
        var translatedSections = ParseTranslatedSections(translation?.TranslatedSections);
        var translatedQuestions = ParseTranslatedQuestions(translation?.TranslatedQuestions);

        // Build section DTOs with progress
        var sections = talk.Sections.Select(section =>
        {
            var progress = scheduledTalk.SectionProgress
                .FirstOrDefault(sp => sp.SectionId == section.Id);

            var translatedSection = translatedSections?
                .FirstOrDefault(ts => ts.SectionId == section.Id);

            return new MyToolboxTalkSectionDto
            {
                SectionId = section.Id,
                SectionNumber = section.SectionNumber,
                Title = translatedSection?.Title ?? section.Title,
                Content = translatedSection?.Content ?? section.Content,
                RequiresAcknowledgment = section.RequiresAcknowledgment,
                IsRead = progress?.IsRead ?? false,
                ReadAt = progress?.ReadAt,
                TimeSpentSeconds = progress?.TimeSpentSeconds ?? 0
            };
        }).ToList();

        // Build question DTOs (without correct answers for quiz taking)
        // Apply quiz randomization if enabled
        var hasRandomization = talk.ShuffleQuestions || talk.ShuffleOptions || talk.UseQuestionPool;
        var lastAttempt = scheduledTalk.QuizAttempts.FirstOrDefault();
        var quizAlreadyPassed = lastAttempt?.Passed == true;

        List<MyToolboxTalkQuestionDto> questions;

        if (hasRandomization && talk.RequiresQuiz && !quizAlreadyPassed)
        {
            // Generate a fresh randomized quiz for each view (new attempt)
            var generatedQuiz = _quizGenerationService.GenerateQuiz(talk);
            questions = BuildRandomizedQuestions(generatedQuiz, talk, translatedQuestions);
        }
        else
        {
            // No randomization or quiz already passed - return questions in original order
            questions = talk.Questions.Select(question =>
            {
                var translatedQuestion = translatedQuestions?
                    .FirstOrDefault(tq => tq.QuestionId == question.Id);

                return new MyToolboxTalkQuestionDto
                {
                    Id = question.Id,
                    QuestionNumber = question.QuestionNumber,
                    QuestionText = translatedQuestion?.QuestionText ?? question.QuestionText,
                    QuestionType = question.QuestionType,
                    QuestionTypeDisplay = GetQuestionTypeDisplay(question.QuestionType),
                    Options = translatedQuestion?.Options ?? ParseOptions(question.Options),
                    Points = question.Points
                };
            }).ToList();
        }

        return new MyToolboxTalkDto
        {
            ScheduledTalkId = scheduledTalk.Id,
            ToolboxTalkId = talk.Id,
            Title = translation?.TranslatedTitle ?? talk.Title,
            Description = translation?.TranslatedDescription ?? talk.Description,
            VideoUrl = talk.VideoUrl,
            VideoSource = talk.VideoSource,
            AttachmentUrl = talk.AttachmentUrl,
            PdfUrl = talk.PdfUrl,
            PdfFileName = talk.PdfFileName,
            MinimumVideoWatchPercent = talk.MinimumVideoWatchPercent,
            RequiresQuiz = talk.RequiresQuiz,
            PassingScore = talk.PassingScore,
            RequiredDate = scheduledTalk.RequiredDate,
            DueDate = scheduledTalk.DueDate,
            Status = scheduledTalk.Status,
            StatusDisplay = GetStatusDisplay(scheduledTalk.Status),
            LanguageCode = languageCode,
            EmployeePreferredLanguage = languageCode,
            TotalSections = sections.Count,
            CompletedSections = sections.Count(s => s.IsRead),
            ProgressPercent = sections.Count > 0
                ? Math.Round((decimal)sections.Count(s => s.IsRead) / sections.Count * 100, 2)
                : 0,
            VideoWatchPercent = scheduledTalk.VideoWatchPercent,
            QuizAttemptCount = scheduledTalk.QuizAttempts.Count,
            LastQuizPassed = lastAttempt?.Passed,
            LastQuizScore = lastAttempt != null && lastAttempt.MaxScore > 0
                ? Math.Round((decimal)lastAttempt.Score / lastAttempt.MaxScore * 100, 2)
                : null,
            HasSlideshow = !string.IsNullOrEmpty(talk.SlideshowHtml),
            Sections = sections,
            Questions = questions,
            CompletedAt = scheduledTalk.Completion?.CompletedAt,
            CertificateUrl = scheduledTalk.Completion?.CertificateUrl,
            IsOverdue = scheduledTalk.Status != ScheduledTalkStatus.Completed && scheduledTalk.DueDate < now,
            DaysUntilDue = (int)Math.Ceiling((scheduledTalk.DueDate - now).TotalDays)
        };
    }

    private List<MyToolboxTalkQuestionDto> BuildRandomizedQuestions(
        RandomizedQuiz generatedQuiz,
        Domain.Entities.ToolboxTalk talk,
        List<TranslatedQuestionData>? translatedQuestions)
    {
        var questionsById = talk.Questions.ToDictionary(q => q.Id);
        var result = new List<MyToolboxTalkQuestionDto>();

        foreach (var generated in generatedQuiz.Questions)
        {
            if (!questionsById.TryGetValue(generated.QuestionId, out var question))
                continue;

            var translatedQuestion = translatedQuestions?
                .FirstOrDefault(tq => tq.QuestionId == question.Id);

            var originalOptions = translatedQuestion?.Options ?? ParseOptions(question.Options);

            // Apply option shuffling if there's a non-default order
            List<string>? displayOptions = null;
            if (originalOptions != null && generated.OptionOrder.Count > 0)
            {
                displayOptions = generated.OptionOrder
                    .Where(idx => idx < originalOptions.Count)
                    .Select(idx => originalOptions[idx])
                    .ToList();
            }
            else
            {
                displayOptions = originalOptions;
            }

            // Include OptionOrder so the frontend can map display index -> original index
            // when submitting answers. This enables translation-safe index-based grading.
            var hasCustomOrder = generated.OptionOrder.Count > 0 &&
                !generated.OptionOrder.SequenceEqual(Enumerable.Range(0, generated.OptionOrder.Count));

            result.Add(new MyToolboxTalkQuestionDto
            {
                Id = question.Id,
                QuestionNumber = generated.DisplayIndex,
                QuestionText = translatedQuestion?.QuestionText ?? question.QuestionText,
                QuestionType = question.QuestionType,
                QuestionTypeDisplay = GetQuestionTypeDisplay(question.QuestionType),
                Options = displayOptions,
                Points = question.Points,
                OptionOriginalIndices = hasCustomOrder ? generated.OptionOrder : null
            });
        }

        return result;
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

    private static string GetStatusDisplay(ScheduledTalkStatus status) => status switch
    {
        ScheduledTalkStatus.Pending => "Pending",
        ScheduledTalkStatus.InProgress => "In Progress",
        ScheduledTalkStatus.Completed => "Completed",
        ScheduledTalkStatus.Overdue => "Overdue",
        ScheduledTalkStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    private static string GetQuestionTypeDisplay(QuestionType type) => type switch
    {
        QuestionType.MultipleChoice => "Multiple Choice",
        QuestionType.TrueFalse => "True/False",
        QuestionType.ShortAnswer => "Short Answer",
        _ => type.ToString()
    };

    // Internal classes for JSON deserialization
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
