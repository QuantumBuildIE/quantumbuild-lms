using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkSections;

public class UpdateToolboxTalkSectionsCommandHandler
    : IRequestHandler<UpdateToolboxTalkSectionsCommand, Result<ToolboxTalkDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public UpdateToolboxTalkSectionsCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<ToolboxTalkDto>> Handle(
        UpdateToolboxTalkSectionsCommand request, CancellationToken ct)
    {
        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == request.TalkId && t.TenantId == request.TenantId && !t.IsDeleted, ct);

        if (talk is null)
            return Result.Fail<ToolboxTalkDto>("Learning not found.");

        var newSections = await UpsertSectionsAsync(talk, request.Sections, ct);

        // Advance to at least step 2; do not regress a further-along wizard
        if ((talk.LastEditedStep ?? 0) < 2)
            talk.LastEditedStep = 2;

        await _dbContext.SaveChangesAsync(ct);

        return Result.Ok(MapToDto(talk, newSections));
    }

    private async Task<List<ToolboxTalkSection>> UpsertSectionsAsync(
        ToolboxTalk talk,
        List<UpdateToolboxTalkSectionDto> sectionDtos,
        CancellationToken ct)
    {
        var existingSections = await _dbContext.ToolboxTalkSections
            .Where(s => s.ToolboxTalkId == talk.Id && !s.IsDeleted)
            .ToListAsync(ct);

        var incomingIds = sectionDtos
            .Where(s => s.Id.HasValue)
            .Select(s => s.Id!.Value)
            .ToHashSet();

        // Soft-delete sections removed by the user
        foreach (var s in existingSections.Where(s => !incomingIds.Contains(s.Id)))
        {
            s.IsDeleted = true;
            s.UpdatedAt = DateTime.UtcNow;
            s.UpdatedBy = _currentUser.UserId;
        }

        var resultSections = new List<ToolboxTalkSection>();

        foreach (var dto in sectionDtos)
        {
            if (dto.Id.HasValue)
            {
                var existing = existingSections.FirstOrDefault(s => s.Id == dto.Id.Value);
                if (existing is not null)
                {
                    existing.SectionNumber = dto.SectionNumber;
                    existing.Title = dto.Title;
                    existing.Content = dto.Content;
                    existing.RequiresAcknowledgment = dto.RequiresAcknowledgment;
                    existing.Source = dto.Source;
                    existing.VideoTimestamp = dto.VideoTimestamp;
                    existing.IsDeleted = false;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = _currentUser.UserId;
                    resultSections.Add(existing);
                    continue;
                }
            }

            // New section (no ID, or ID not found in DB)
            var newSection = new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = talk.Id,
                SectionNumber = dto.SectionNumber,
                Title = dto.Title,
                Content = dto.Content,
                RequiresAcknowledgment = dto.RequiresAcknowledgment,
                Source = dto.Source,
                VideoTimestamp = dto.VideoTimestamp,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId,
            };
            _dbContext.ToolboxTalkSections.Add(newSection);
            resultSections.Add(newSection);
        }

        return resultSections;
    }

    private static ToolboxTalkDto MapToDto(ToolboxTalk talk, List<ToolboxTalkSection> sections)
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
            Sections = sections.OrderBy(s => s.SectionNumber).Select(s => new ToolboxTalkSectionDto
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
            Questions = [],
            Translations = [],
        };
    }
}
