using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkQuestions;

public class UpdateToolboxTalkQuestionsCommandHandler
    : IRequestHandler<UpdateToolboxTalkQuestionsCommand, Result<ToolboxTalkDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public UpdateToolboxTalkQuestionsCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<ToolboxTalkDto>> Handle(
        UpdateToolboxTalkQuestionsCommand request, CancellationToken ct)
    {
        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == request.TalkId && t.TenantId == request.TenantId && !t.IsDeleted, ct);

        if (talk is null)
            return Result.Fail<ToolboxTalkDto>("Learning not found.");

        if (talk.Status != ToolboxTalkStatus.Draft)
            return Result.Fail<ToolboxTalkDto>(
                "Learning must be in Draft status to update questions.",
                FailureCode.WorkflowInvalidState);

        var updatedQuestions = await UpsertQuestionsAsync(talk, request.Questions, ct);

        if ((talk.LastEditedStep ?? 0) < 3)
            talk.LastEditedStep = 3;

        await _dbContext.SaveChangesAsync(ct);

        return Result.Ok(MapToDto(talk, updatedQuestions));
    }

    private async Task<List<ToolboxTalkQuestion>> UpsertQuestionsAsync(
        ToolboxTalk talk,
        List<UpdateToolboxTalkQuestionDto> questionDtos,
        CancellationToken ct)
    {
        var incomingIds = questionDtos
            .Where(q => q.Id.HasValue)
            .Select(q => q.Id!.Value)
            .ToHashSet();

        // Hard-delete questions omitted from the incoming list
        await _dbContext.ToolboxTalkQuestions
            .Where(q => q.ToolboxTalkId == talk.Id && !incomingIds.Contains(q.Id))
            .ExecuteDeleteAsync(ct);

        // Load remaining questions for in-place update
        var existing = incomingIds.Count > 0
            ? await _dbContext.ToolboxTalkQuestions
                .Where(q => q.ToolboxTalkId == talk.Id)
                .ToListAsync(ct)
            : [];

        var result = new List<ToolboxTalkQuestion>();

        foreach (var dto in questionDtos)
        {
            var optionsList = dto.Options ?? [];
            var correctOptionIdx = dto.CorrectOptionIndex ?? 0;
            var correctAnswer = optionsList.Count > 0 && correctOptionIdx < optionsList.Count
                ? optionsList[correctOptionIdx]
                : dto.CorrectAnswer ?? string.Empty;
            var optionsJson = optionsList.Count > 0
                ? JsonSerializer.Serialize(optionsList)
                : null;

            if (dto.Id.HasValue)
            {
                var q = existing.FirstOrDefault(x => x.Id == dto.Id.Value);
                if (q is not null)
                {
                    q.QuestionNumber = dto.QuestionNumber;
                    q.QuestionText = dto.QuestionText;
                    q.QuestionType = dto.QuestionType;
                    q.Options = optionsJson;
                    q.CorrectAnswer = correctAnswer;
                    q.CorrectOptionIndex = dto.CorrectOptionIndex;
                    q.Points = dto.Points;
                    q.Source = dto.Source;
                    q.IsFromVideoFinalPortion = dto.IsFromVideoFinalPortion;
                    q.VideoTimestamp = dto.VideoTimestamp;
                    q.UpdatedAt = DateTime.UtcNow;
                    q.UpdatedBy = _currentUser.UserId;
                    result.Add(q);
                    continue;
                }
            }

            var newQ = new ToolboxTalkQuestion
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = talk.Id,
                QuestionNumber = dto.QuestionNumber,
                QuestionText = dto.QuestionText,
                QuestionType = dto.QuestionType,
                Options = optionsJson,
                CorrectAnswer = correctAnswer,
                CorrectOptionIndex = dto.CorrectOptionIndex,
                Points = dto.Points,
                Source = dto.Source,
                IsFromVideoFinalPortion = dto.IsFromVideoFinalPortion,
                VideoTimestamp = dto.VideoTimestamp,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId,
            };
            _dbContext.ToolboxTalkQuestions.Add(newQ);
            result.Add(newQ);
        }

        return result;
    }

    private static ToolboxTalkDto MapToDto(ToolboxTalk talk, List<ToolboxTalkQuestion> questions)
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
            Sections = [],
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
