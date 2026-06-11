using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkQuizSettings;

public class UpdateToolboxTalkQuizSettingsCommandHandler
    : IRequestHandler<UpdateToolboxTalkQuizSettingsCommand, Result<ToolboxTalkDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;

    public UpdateToolboxTalkQuizSettingsCommandHandler(IToolboxTalksDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<ToolboxTalkDto>> Handle(
        UpdateToolboxTalkQuizSettingsCommand request, CancellationToken ct)
    {
        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == request.TalkId && t.TenantId == request.TenantId && !t.IsDeleted, ct);

        if (talk is null)
            return Result.Fail<ToolboxTalkDto>("Learning not found.");

        if (talk.Status != ToolboxTalkStatus.Draft)
            return Result.Fail<ToolboxTalkDto>(
                "Learning must be in Draft status to update quiz settings.",
                FailureCode.WorkflowInvalidState);

        talk.RequiresQuiz = request.RequiresQuiz;
        talk.PassingScore = request.PassingScore;
        talk.QuizQuestionCount = request.QuizQuestionCount;
        talk.ShuffleQuestions = request.ShuffleQuestions;
        talk.ShuffleOptions = request.ShuffleOptions;
        talk.UseQuestionPool = request.UseQuestionPool;
        talk.AllowRetry = request.AllowRetry;

        if ((talk.LastEditedStep ?? 0) < 3)
            talk.LastEditedStep = 3;

        await _dbContext.SaveChangesAsync(ct);

        var sections = await _dbContext.ToolboxTalkSections
            .Where(s => s.ToolboxTalkId == talk.Id && !s.IsDeleted)
            .OrderBy(s => s.SectionNumber)
            .ToListAsync(ct);

        var questions = await _dbContext.ToolboxTalkQuestions
            .Where(q => q.ToolboxTalkId == talk.Id)
            .OrderBy(q => q.QuestionNumber)
            .ToListAsync(ct);

        return Result.Ok(MapToDto(talk, sections, questions));
    }

    private static ToolboxTalkDto MapToDto(
        Domain.Entities.ToolboxTalk talk,
        List<Domain.Entities.ToolboxTalkSection> sections,
        List<Domain.Entities.ToolboxTalkQuestion> questions)
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
            Questions = questions.Select(q => new ToolboxTalkQuestionDto
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
