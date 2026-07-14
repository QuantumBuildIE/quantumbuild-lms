using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateToolboxTalkQuiz;

public class GenerateToolboxTalkQuizCommandHandler
    : IRequestHandler<GenerateToolboxTalkQuizCommand, Result<ToolboxTalkDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IAiQuizGenerationService _aiQuizGenerationService;
    private readonly ILogger<GenerateToolboxTalkQuizCommandHandler> _logger;

    public GenerateToolboxTalkQuizCommandHandler(
        IToolboxTalksDbContext dbContext,
        IAiQuizGenerationService aiQuizGenerationService,
        ILogger<GenerateToolboxTalkQuizCommandHandler> logger)
    {
        _dbContext = dbContext;
        _aiQuizGenerationService = aiQuizGenerationService;
        _logger = logger;
    }

    public async Task<Result<ToolboxTalkDto>> Handle(
        GenerateToolboxTalkQuizCommand request, CancellationToken ct)
    {
        var talk = await _dbContext.ToolboxTalks
            .Where(t => t.Id == request.TalkId && t.TenantId == request.TenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (talk is null)
            return Result.Fail<ToolboxTalkDto>("Learning not found.");

        if (talk.Status != ToolboxTalkStatus.Draft)
            return Result.Fail<ToolboxTalkDto>(
                "Learning must be in Draft status to generate a quiz.",
                FailureCode.WorkflowInvalidState);

        var sections = await _dbContext.ToolboxTalkSections
            .Where(s => s.ToolboxTalkId == talk.Id && !s.IsDeleted)
            .OrderBy(s => s.SectionNumber)
            .ToListAsync(ct);

        if (sections.Count == 0)
            return Result.Fail<ToolboxTalkDto>("Cannot generate a quiz without sections. Parse content first.");

        var combinedContent = BuildCombinedContent(sections);
        var hasVideoContent = talk.InputMode == InputMode.Video;
        var hasPdfContent = talk.InputMode == InputMode.Pdf;
        var audienceRole = string.IsNullOrWhiteSpace(talk.AudienceRole) ? "Operator" : talk.AudienceRole;

        talk.Status = ToolboxTalkStatus.Processing;
        await _dbContext.SaveChangesAsync(ct);

        var quizResult = await _aiQuizGenerationService.GenerateQuizAsync(
            toolboxTalkId: talk.Id,
            combinedContent: combinedContent,
            videoFinalPortionContent: null,
            hasVideoContent: hasVideoContent,
            hasPdfContent: hasPdfContent,
            tenantId: request.TenantId,
            userId: request.UserId,
            minimumQuestions: 5,
            audienceRole: audienceRole,
            cancellationToken: ct);

        if (!quizResult.Success || quizResult.Questions.Count == 0)
        {
            // Return talk to Draft so the user can retry
            talk.Status = ToolboxTalkStatus.Draft;
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogWarning("Quiz generation failed for talk {TalkId}: {Error}",
                talk.Id, quizResult.ErrorMessage);

            return Result.Fail<ToolboxTalkDto>(
                quizResult.ErrorMessage ?? "Quiz generation failed. Please try again.");
        }

        var newQuestions = await MaterialiseQuestionsAsync(talk, quizResult.Questions, ct);

        talk.Status = ToolboxTalkStatus.Draft;
        if ((talk.LastEditedStep ?? 0) < 3)
            talk.LastEditedStep = 3;

        await _dbContext.SaveChangesAsync(ct);

        return Result.Ok(MapToDto(talk, sections, newQuestions));
    }

    private static string BuildCombinedContent(List<ToolboxTalkSection> sections)
    {
        return string.Join("\n\n", sections.Select(s =>
            $"## {s.Title}\n\n{s.Content}"));
    }

    private async Task<List<ToolboxTalkQuestion>> MaterialiseQuestionsAsync(
        ToolboxTalk talk,
        List<GeneratedQuizQuestion> generatedQuestions,
        CancellationToken ct)
    {
        // Hard-delete all existing questions for this talk (replacing, not appending)
        await _dbContext.ToolboxTalkQuestions
            .Where(q => q.ToolboxTalkId == talk.Id)
            .ExecuteDeleteAsync(ct);

        var newQuestions = new List<ToolboxTalkQuestion>();
        var questionNumber = 1;

        foreach (var gq in generatedQuestions.OrderBy(q => q.SortOrder))
        {
            var optionsJson = gq.Options.Count > 0
                ? JsonSerializer.Serialize(gq.Options)
                : null;

            var correctAnswer = gq.Options.Count > 0 && gq.CorrectAnswerIndex < gq.Options.Count
                ? gq.Options[gq.CorrectAnswerIndex]
                : string.Empty;

            var question = new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = talk.Id,
                QuestionNumber = questionNumber++,
                QuestionText = gq.QuestionText,
                QuestionType = QuestionType.MultipleChoice,
                Options = optionsJson,
                CorrectAnswer = correctAnswer,
                CorrectOptionIndex = gq.CorrectAnswerIndex,
                Points = 1,
                Source = gq.Source,
                IsFromVideoFinalPortion = gq.IsFromVideoFinalPortion,
                VideoTimestamp = gq.VideoTimestamp,
            };

            _dbContext.ToolboxTalkQuestions.Add(question);
            newQuestions.Add(question);
        }

        return newQuestions;
    }

    private static ToolboxTalkDto MapToDto(
        ToolboxTalk talk,
        List<ToolboxTalkSection> sections,
        List<ToolboxTalkQuestion> questions)
    {
        return new ToolboxTalkDto
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
            CreatedAt = talk.CreatedAt,
            UpdatedAt = talk.UpdatedAt,
            Sections = sections.Select(s => new ToolboxTalkSectionDto
            {
                Id = s.Id,
                ToolboxTalkId = s.ToolboxTalkId,
                SectionNumber = s.SectionNumber,
                Title = s.Title,
                Content = s.Content,
                RequiresAcknowledgment = s.RequiresAcknowledgment,
                Source = s.Source,
                SourceDisplay = s.Source switch
                {
                    ContentSource.Manual => "Manual",
                    ContentSource.Video => "Video",
                    ContentSource.Pdf => "PDF",
                    _ => s.Source.ToString()
                },
                VideoTimestamp = s.VideoTimestamp,
            }).ToList(),
            Questions = questions.OrderBy(q => q.QuestionNumber).Select(q => new ToolboxTalkQuestionDto
            {
                Id = q.Id,
                ToolboxTalkId = q.ToolboxTalkId,
                QuestionNumber = q.QuestionNumber,
                QuestionText = q.QuestionText,
                QuestionType = q.QuestionType,
                QuestionTypeDisplay = q.QuestionType switch
                {
                    QuestionType.MultipleChoice => "Multiple Choice",
                    QuestionType.TrueFalse => "True/False",
                    QuestionType.ShortAnswer => "Short Answer",
                    _ => q.QuestionType.ToString()
                },
                Options = string.IsNullOrEmpty(q.Options)
                    ? null
                    : JsonSerializer.Deserialize<List<string>>(q.Options),
                CorrectAnswer = q.CorrectAnswer,
                CorrectOptionIndex = q.CorrectOptionIndex,
                Points = q.Points,
                Source = q.Source,
                SourceDisplay = q.Source switch
                {
                    ContentSource.Manual => "Manual",
                    ContentSource.Video => "Video",
                    ContentSource.Pdf => "PDF",
                    _ => q.Source.ToString()
                },
                IsFromVideoFinalPortion = q.IsFromVideoFinalPortion,
                VideoTimestamp = q.VideoTimestamp,
            }).ToList(),
            Translations = [],
        };
    }
}
