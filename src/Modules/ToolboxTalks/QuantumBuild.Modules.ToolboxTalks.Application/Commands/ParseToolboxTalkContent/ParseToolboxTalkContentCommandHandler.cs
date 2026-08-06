using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.ParseToolboxTalkContent;

public class ParseToolboxTalkContentCommandHandler
    : IRequestHandler<ParseToolboxTalkContentCommand, Result<ToolboxTalkDto>>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IContentParserService _contentParserService;
    private readonly IPdfExtractionService _pdfExtractionService;
    private readonly IDocxExtractionService _docxExtractionService;
    private readonly IParseJobScheduler _parseJobScheduler;

    public ParseToolboxTalkContentCommandHandler(
        IToolboxTalksDbContext dbContext,
        IContentParserService contentParserService,
        IPdfExtractionService pdfExtractionService,
        IDocxExtractionService docxExtractionService,
        IParseJobScheduler parseJobScheduler)
    {
        _dbContext = dbContext;
        _contentParserService = contentParserService;
        _pdfExtractionService = pdfExtractionService;
        _docxExtractionService = docxExtractionService;
        _parseJobScheduler = parseJobScheduler;
    }

    public async Task<Result<ToolboxTalkDto>> Handle(
        ParseToolboxTalkContentCommand request, CancellationToken ct)
    {
        var talk = await _dbContext.ToolboxTalks
            .Where(t => t.Id == request.TalkId && t.TenantId == request.TenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (talk is null)
            return Result.Fail<ToolboxTalkDto>("Learning not found.");

        if (talk.Status != ToolboxTalkStatus.Draft)
            return Result.Fail<ToolboxTalkDto>(
                "Learning must be in Draft status to parse content.",
                FailureCode.WorkflowInvalidState);

        return talk.InputMode switch
        {
            InputMode.Pdf => await HandlePdfAsync(talk, request.UserId, ct),
            InputMode.Video => await HandleVideoAsync(talk, ct),
            InputMode.Docx => await HandleDocxAsync(talk, request.UserId, ct),
            _ => await HandleTextAsync(talk, request.UserId, ct),
        };
    }

    private async Task<Result<ToolboxTalkDto>> HandleTextAsync(
        ToolboxTalk talk, Guid? userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(talk.SourceText))
            return Result.Fail<ToolboxTalkDto>("Source text is required for Text mode.");

        talk.Status = ToolboxTalkStatus.Processing;
        await _dbContext.SaveChangesAsync(ct);

        var parseResult = await _contentParserService.ParseContentAsync(
            talk.SourceText, InputMode.Text, talk.TenantId, userId,
            talk.PreserveSourceWording, ct);

        if (!parseResult.Success)
        {
            talk.Status = ToolboxTalkStatus.Draft;
            await _dbContext.SaveChangesAsync(ct);
            return Result.Fail<ToolboxTalkDto>(parseResult.ErrorMessage ?? "Content parsing failed.");
        }

        var newSections = await MaterialiseSectionsAsync(
            talk, parseResult.Sections, ContentSource.Manual, ct);

        talk.Status = ToolboxTalkStatus.Draft;
        talk.LastEditedStep = 2;
        await _dbContext.SaveChangesAsync(ct);

        return Result.Ok(MapToDto(talk, newSections));
    }

    private async Task<Result<ToolboxTalkDto>> HandlePdfAsync(
        ToolboxTalk talk, Guid? userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(talk.SourceFileUrl))
            return Result.Fail<ToolboxTalkDto>("Source file URL is required for PDF mode.");

        talk.Status = ToolboxTalkStatus.Processing;
        await _dbContext.SaveChangesAsync(ct);

        var extractResult = await _pdfExtractionService.ExtractTextFromUrlAsync(talk.SourceFileUrl, ct);
        if (!extractResult.Success)
        {
            talk.Status = ToolboxTalkStatus.Draft;
            await _dbContext.SaveChangesAsync(ct);
            return Result.Fail<ToolboxTalkDto>(
                extractResult.ErrorMessage ?? "PDF text extraction failed.");
        }

        talk.ExtractedPdfText = extractResult.Text;
        talk.PdfTextExtractedAt = DateTime.UtcNow;

        var parseResult = await _contentParserService.ParseContentAsync(
            extractResult.Text!, InputMode.Pdf, talk.TenantId, userId,
            talk.PreserveSourceWording, ct);

        if (!parseResult.Success)
        {
            talk.Status = ToolboxTalkStatus.Draft;
            await _dbContext.SaveChangesAsync(ct);
            return Result.Fail<ToolboxTalkDto>(parseResult.ErrorMessage ?? "Content parsing failed.");
        }

        var newSections = await MaterialiseSectionsAsync(
            talk, parseResult.Sections, ContentSource.Pdf, ct);

        talk.GeneratedFromPdf = true;
        talk.Status = ToolboxTalkStatus.Draft;
        talk.LastEditedStep = 2;
        await _dbContext.SaveChangesAsync(ct);

        return Result.Ok(MapToDto(talk, newSections));
    }

    private async Task<Result<ToolboxTalkDto>> HandleDocxAsync(
        ToolboxTalk talk, Guid? userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(talk.SourceFileUrl))
            return Result.Fail<ToolboxTalkDto>("Source file URL is required for Word document mode.");

        talk.Status = ToolboxTalkStatus.Processing;
        await _dbContext.SaveChangesAsync(ct);

        var extractResult = await _docxExtractionService.ExtractTextFromUrlAsync(talk.SourceFileUrl, ct);
        if (!extractResult.Success)
        {
            talk.Status = ToolboxTalkStatus.Draft;
            await _dbContext.SaveChangesAsync(ct);
            return Result.Fail<ToolboxTalkDto>(
                extractResult.ErrorMessage ?? "Word document text extraction failed.");
        }

        var parseResult = await _contentParserService.ParseContentAsync(
            extractResult.Text!, InputMode.Docx, talk.TenantId, userId,
            talk.PreserveSourceWording, ct);

        if (!parseResult.Success)
        {
            talk.Status = ToolboxTalkStatus.Draft;
            await _dbContext.SaveChangesAsync(ct);
            return Result.Fail<ToolboxTalkDto>(parseResult.ErrorMessage ?? "Content parsing failed.");
        }

        var newSections = await MaterialiseSectionsAsync(
            talk, parseResult.Sections, ContentSource.Docx, ct);

        talk.Status = ToolboxTalkStatus.Draft;
        talk.LastEditedStep = 2;
        await _dbContext.SaveChangesAsync(ct);

        return Result.Ok(MapToDto(talk, newSections));
    }

    private async Task<Result<ToolboxTalkDto>> HandleVideoAsync(ToolboxTalk talk, CancellationToken ct)
    {
        // Determine the video URL — uploaded file takes precedence over VideoUrl
        var videoUrl = talk.SourceFileType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true
            ? talk.SourceFileUrl
            : talk.VideoUrl;

        if (string.IsNullOrWhiteSpace(videoUrl))
            return Result.Fail<ToolboxTalkDto>(
                "No video URL is available for transcription. Upload a video or set a video URL.");

        talk.Status = ToolboxTalkStatus.Processing;
        talk.LastEditedStep = 2;
        await _dbContext.SaveChangesAsync(ct);

        _parseJobScheduler.EnqueueVideoTranscriptionJob(talk.Id, talk.TenantId);

        return Result.Ok(MapToDto(talk, []));
    }

    private async Task<List<ToolboxTalkSection>> MaterialiseSectionsAsync(
        ToolboxTalk talk,
        List<ParsedSection> parsedSections,
        ContentSource source,
        CancellationToken ct)
    {
        // Soft-delete all existing sections
        var existing = await _dbContext.ToolboxTalkSections
            .Where(s => s.ToolboxTalkId == talk.Id && !s.IsDeleted)
            .ToListAsync(ct);

        foreach (var s in existing)
            s.IsDeleted = true;

        // Insert fresh sections in suggested order
        var newSections = new List<ToolboxTalkSection>();
        var sectionNumber = 1;
        foreach (var parsed in parsedSections.OrderBy(s => s.SuggestedOrder))
        {
            var section = new ToolboxTalkSection
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = talk.Id,
                SectionNumber = sectionNumber++,
                Title = parsed.Title,
                Content = parsed.Content,
                RequiresAcknowledgment = true,
                Source = source,
            };
            _dbContext.ToolboxTalkSections.Add(section);
            newSections.Add(section);
        }

        return newSections;
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
                    ContentSource.Docx => "Word",
                    _ => s.Source.ToString()
                },
                VideoTimestamp = s.VideoTimestamp,
            }).ToList(),
            Questions = [],
            Translations = [],
        };
    }
}
