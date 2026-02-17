using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.SmartGenerateContent;

/// <summary>
/// Handles smart content generation by checking for duplicate sources,
/// copying existing content, and determining what needs AI generation.
///
/// The reuse step runs synchronously. If AI generation is needed,
/// the result indicates what's missing so the caller (controller) can
/// queue a background job.
/// </summary>
public class SmartGenerateContentCommandHandler
    : IRequestHandler<SmartGenerateContentCommand, SmartGenerateContentResult>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly IContentDeduplicationService _deduplicationService;
    private readonly ILogger<SmartGenerateContentCommandHandler> _logger;

    public SmartGenerateContentCommandHandler(
        IToolboxTalksDbContext context,
        IContentDeduplicationService deduplicationService,
        ILogger<SmartGenerateContentCommandHandler> logger)
    {
        _context = context;
        _deduplicationService = deduplicationService;
        _logger = logger;
    }

    public async Task<SmartGenerateContentResult> Handle(
        SmartGenerateContentCommand request,
        CancellationToken ct)
    {
        var result = new SmartGenerateContentResult();

        // Load the target talk
        var talk = await _context.ToolboxTalks
            .FirstOrDefaultAsync(
                t => t.Id == request.ToolboxTalkId
                     && t.TenantId == request.TenantId
                     && !t.IsDeleted, ct);

        if (talk == null)
        {
            result.Success = false;
            result.ErrorMessage = "Learning not found";
            return result;
        }

        // Save source language before any generation
        talk.SourceLanguageCode = request.SourceLanguageCode;
        await _context.SaveChangesAsync(ct);

        // Determine which file to check for duplicates
        string? fileHash = null;
        var fileType = FileHashType.Pdf;

        if (request.IncludePdf && !string.IsNullOrEmpty(talk.PdfFileHash))
        {
            fileHash = talk.PdfFileHash;
            fileType = FileHashType.Pdf;
        }
        else if (request.IncludePdf && !string.IsNullOrEmpty(talk.PdfUrl))
        {
            // Calculate hash from URL if not already stored
            fileHash = await _deduplicationService.CalculateFileHashFromUrlAsync(talk.PdfUrl, ct);
            fileType = FileHashType.Pdf;
        }
        else if (request.IncludeVideo && !string.IsNullOrEmpty(talk.VideoFileHash))
        {
            fileHash = talk.VideoFileHash;
            fileType = FileHashType.Video;
        }
        else if (request.IncludeVideo && !string.IsNullOrEmpty(talk.VideoUrl))
        {
            fileHash = await _deduplicationService.CalculateFileHashFromUrlAsync(talk.VideoUrl, ct);
            fileType = FileHashType.Video;
        }

        // Check for duplicate
        DuplicateCheckResult? duplicateCheck = null;
        if (!string.IsNullOrEmpty(fileHash))
        {
            duplicateCheck = await _deduplicationService.CheckForDuplicateAsync(
                request.TenantId, fileHash, fileType, request.ToolboxTalkId, ct);
        }

        var needToGenerateSections = request.GenerateSections;
        var needToGenerateQuestions = request.GenerateQuestions;
        var needToGenerateSlideshow = request.GenerateSlideshow;

        if (duplicateCheck?.IsDuplicate == true && duplicateCheck.SourceToolboxTalk != null)
        {
            var source = duplicateCheck.SourceToolboxTalk;
            result.ContentCopiedFromTitle = source.Title;

            _logger.LogInformation(
                "Found duplicate source {SourceId} ({SourceTitle}) for talk {TalkId}. " +
                "Sections: {HasSections} ({SectionCount}), Questions: {HasQuestions} ({QuestionCount}), Slideshow: {HasSlideshow}",
                source.Id, source.Title, request.ToolboxTalkId,
                source.SectionCount > 0, source.SectionCount,
                source.QuestionCount > 0, source.QuestionCount,
                source.HasSlideshow);

            // Determine what to copy vs generate
            var reuseOptions = new ReuseContentOptions
            {
                CopySections = request.GenerateSections && source.SectionCount > 0,
                CopyQuestions = request.GenerateQuestions && source.QuestionCount > 0,
                CopySlideshow = request.GenerateSlideshow && source.HasSlideshow,
                CopyTranslations = true // Always copy translations if available
            };

            // Copy what exists
            if (reuseOptions.CopySections || reuseOptions.CopyQuestions ||
                reuseOptions.CopySlideshow || reuseOptions.CopyTranslations)
            {
                var reuseResult = await _deduplicationService.ReuseContentAsync(
                    request.ToolboxTalkId,
                    source.Id,
                    request.TenantId,
                    reuseOptions,
                    ct);

                if (reuseResult.Success)
                {
                    result.SectionsCopied = reuseResult.SectionsCopied;
                    result.QuestionsCopied = reuseResult.QuestionsCopied;
                    result.SlideshowCopied = reuseResult.SlideshowCopied;
                    result.TranslationsCopied = reuseResult.TranslationsCopied;
                    result.SubtitlesCopied = reuseResult.SubtitlesCopied;
                }
                else
                {
                    _logger.LogWarning(
                        "Content reuse failed for talk {TalkId}: {Error}. Falling back to generation.",
                        request.ToolboxTalkId, reuseResult.ErrorMessage);
                    // Fall through to generate everything
                }
            }

            // Determine what still needs AI generation
            needToGenerateSections = request.GenerateSections && source.SectionCount == 0;
            needToGenerateQuestions = request.GenerateQuestions && source.QuestionCount == 0;
            needToGenerateSlideshow = request.GenerateSlideshow && !source.HasSlideshow;
        }
        else
        {
            _logger.LogInformation(
                "No duplicate found for talk {TalkId}, all requested content needs generation",
                request.ToolboxTalkId);
        }

        // Update file hash for future deduplication
        if (!string.IsNullOrEmpty(fileHash))
        {
            await _deduplicationService.UpdateFileHashAsync(
                request.ToolboxTalkId, fileHash, fileType, request.TenantId, ct);
        }

        // If anything needs AI generation, signal the caller to queue a background job
        if (needToGenerateSections || needToGenerateQuestions || needToGenerateSlideshow)
        {
            // The controller will queue the Hangfire job based on these flags
            result.GenerationJobQueued = true;

            _logger.LogInformation(
                "AI generation needed for talk {TalkId}. Sections: {Sections}, Questions: {Questions}, Slideshow: {Slideshow}",
                request.ToolboxTalkId, needToGenerateSections, needToGenerateQuestions, needToGenerateSlideshow);
        }

        result.Success = true;
        return result;
    }
}
