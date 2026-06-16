using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Domain.Helpers;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkSettings;

public class UpdateToolboxTalkSettingsCommandHandler
    : IRequestHandler<UpdateToolboxTalkSettingsCommand, Result<ToolboxTalkDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ITranslationWorkflowService _workflowService;

    public UpdateToolboxTalkSettingsCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser,
        ITranslationWorkflowService workflowService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _workflowService = workflowService;
    }

    public async Task<Result<ToolboxTalkDto>> Handle(
        UpdateToolboxTalkSettingsCommand request, CancellationToken ct)
    {
        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(
                t => t.Id == request.TalkId && t.TenantId == request.TenantId && !t.IsDeleted, ct);

        if (talk is null)
            return Result.Fail<ToolboxTalkDto>("Learning not found.");

        if (talk.Status != ToolboxTalkStatus.Draft)
            return Result.Fail<ToolboxTalkDto>(
                "Learning must be in Draft status to update settings.",
                FailureCode.WorkflowInvalidState);

        // Title uniqueness check — exclude current talk
        if (!string.Equals(talk.Title, request.Title, StringComparison.Ordinal))
        {
            var titleTaken = await _dbContext.ToolboxTalks
                .AnyAsync(t => t.TenantId == request.TenantId
                            && t.Title == request.Title
                            && t.Id != request.TalkId
                            && !t.IsDeleted, ct);

            if (titleTaken)
                return Result.Fail<ToolboxTalkDto>(
                    $"A learning with the title '{request.Title}' already exists.",
                    FailureCode.TitleNotUnique);
        }

        // Capture translated scalar fields BEFORE mutation for stalening detection
        var oldTitle = talk.Title;
        var oldDescription = talk.Description;

        // Apply all settings fields
        talk.Title = request.Title;
        talk.Description = request.Description;
        talk.Category = request.Category;
        talk.IsActive = request.IsActive;
        talk.GenerateCertificate = request.GenerateCertificate;
        talk.MinimumVideoWatchPercent = request.MinimumVideoWatchPercent;
        talk.AutoAssignToNewEmployees = request.AutoAssignToNewEmployees;
        talk.AutoAssignDueDays = request.AutoAssignDueDays;
        talk.GenerateSlidesFromPdf = request.GenerateSlidesFromPdf;
        talk.UpdatedAt = DateTime.UtcNow;
        talk.UpdatedBy = _currentUser.UserId;

        // Translate wizard RefresherFrequency → canonical entity fields, preserving the
        // existing interval when Once is selected so a user who switches back to a
        // non-Once value later gets the same interval restored.
        (talk.RequiresRefresher, talk.RefresherIntervalMonths) =
            RefresherFrequencyMapper.FromWizardFrequencyString(
                request.RefresherFrequency.ToString(), talk.RefresherIntervalMonths);

        // Keep legacy Frequency in sync so dashboard breakdown and old edit form stay correct
        talk.Frequency = RefresherFrequencyMapper.ToLegacyFrequency(talk.RequiresRefresher, talk.RefresherIntervalMonths);

        if ((talk.LastEditedStep ?? 0) < 4)
            talk.LastEditedStep = 4;

        // Collect languages with existing translations before saving
        // (MarkStale is a no-op when no translations exist — safe at all wizard stages)
        var staledLanguageCodes = new List<string>();
        var scalarStaleningChange = oldTitle != request.Title || oldDescription != request.Description;

        if (scalarStaleningChange)
        {
            var affectedTranslations = await _dbContext.ToolboxTalkTranslations
                .Where(t => t.ToolboxTalkId == talk.Id && !t.IsDeleted)
                .ToListAsync(ct);

            foreach (var translation in affectedTranslations)
            {
                if (!translation.NeedsRevalidation)
                    translation.NeedsRevalidation = true;
                staledLanguageCodes.Add(translation.LanguageCode);
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // Emit MarkedStale workflow events after the save (idempotent per Phase 3a)
        foreach (var languageCode in staledLanguageCodes)
        {
            await _workflowService.MarkStale(talk.Id, languageCode, ct: ct);
        }

        var sections = await _dbContext.ToolboxTalkSections
            .Where(s => s.ToolboxTalkId == talk.Id && !s.IsDeleted)
            .OrderBy(s => s.SectionNumber)
            .ToListAsync(ct);

        var questions = await _dbContext.ToolboxTalkQuestions
            .Where(q => q.ToolboxTalkId == talk.Id && !q.IsDeleted)
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
            CoverImageUrl = talk.CoverImageUrl,
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
