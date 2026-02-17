using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Implementation of the content deduplication service.
/// Handles file hashing, duplicate detection, and content reuse.
/// </summary>
public class ContentDeduplicationService : IContentDeduplicationService
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ContentDeduplicationService> _logger;

    public ContentDeduplicationService(
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser,
        IHttpClientFactory httpClientFactory,
        ILogger<ContentDeduplicationService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public string CalculateFileHash(Stream fileStream)
    {
        using var sha256 = SHA256.Create();
        fileStream.Position = 0;
        var hashBytes = sha256.ComputeHash(fileStream);
        fileStream.Position = 0; // Reset for subsequent operations
        return Convert.ToHexString(hashBytes);
    }

    /// <inheritdoc />
    public async Task<string> CalculateFileHashFromUrlAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating file hash from URL: {FileUrl}", fileUrl);

        var client = _httpClientFactory.CreateClient();

        // Use streaming to avoid loading entire file into memory
        using var response = await client.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var sha256 = SHA256.Create();
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        var hash = Convert.ToHexString(hashBytes);

        _logger.LogDebug("Calculated file hash: {Hash}", hash);
        return hash;
    }

    /// <inheritdoc />
    public async Task<DuplicateCheckResult> CheckForDuplicateAsync(
        Guid tenantId,
        string fileHash,
        FileHashType fileType,
        Guid excludeToolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Checking for duplicate content. TenantId: {TenantId}, FileType: {FileType}, Hash: {Hash}, ExcludeId: {ExcludeId}",
            tenantId, fileType, fileHash, excludeToolboxTalkId);

        // Build the query based on file type
        var query = _dbContext.ToolboxTalks
            .Include(t => t.Sections.Where(s => !s.IsDeleted))
            .Include(t => t.Questions.Where(q => !q.IsDeleted))
            .Include(t => t.Translations.Where(tr => !tr.IsDeleted))
            .Where(t => t.TenantId == tenantId && t.Id != excludeToolboxTalkId && !t.IsDeleted);

        if (fileType == FileHashType.Pdf)
        {
            query = query.Where(t => t.PdfFileHash == fileHash);
        }
        else
        {
            query = query.Where(t => t.VideoFileHash == fileHash);
        }

        var existingTalk = await query.FirstOrDefaultAsync(cancellationToken);

        if (existingTalk == null)
        {
            _logger.LogInformation("No duplicate found for hash: {Hash}", fileHash);
            return new DuplicateCheckResult
            {
                IsDuplicate = false,
                SourceToolboxTalk = null
            };
        }

        _logger.LogInformation(
            "Duplicate found! SourceId: {SourceId}, Title: {Title}, Sections: {Sections}, Questions: {Questions}",
            existingTalk.Id, existingTalk.Title, existingTalk.Sections.Count, existingTalk.Questions.Count);

        // Extract translation languages
        var translationLanguages = existingTalk.Translations
            .Select(t => GetLanguageName(t.LanguageCode))
            .Distinct()
            .ToList();

        // Check if source has completed subtitle processing
        var hasSubtitles = await _dbContext.SubtitleProcessingJobs
            .AnyAsync(j => j.ToolboxTalkId == existingTalk.Id
                && !j.IsDeleted
                && j.Status == Domain.Enums.SubtitleProcessingStatus.Completed, cancellationToken);

        return new DuplicateCheckResult
        {
            IsDuplicate = true,
            SourceToolboxTalk = new SourceToolboxTalkInfo
            {
                Id = existingTalk.Id,
                Title = existingTalk.Title,
                ProcessedAt = existingTalk.ContentGeneratedAt,
                SectionCount = existingTalk.Sections.Count,
                QuestionCount = existingTalk.Questions.Count,
                HasSlideshow = !string.IsNullOrEmpty(existingTalk.SlideshowHtml),
                HasSubtitles = hasSubtitles,
                TranslationLanguages = translationLanguages
            }
        };
    }

    /// <inheritdoc />
    public async Task<ContentReuseResult> ReuseContentAsync(
        Guid targetToolboxTalkId,
        Guid sourceToolboxTalkId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting content reuse. TargetId: {TargetId}, SourceId: {SourceId}, TenantId: {TenantId}",
            targetToolboxTalkId, sourceToolboxTalkId, tenantId);

        try
        {
            // Load source toolbox talk with all content
            var source = await _dbContext.ToolboxTalks
                .Include(t => t.Sections.Where(s => !s.IsDeleted))
                .Include(t => t.Questions.Where(q => !q.IsDeleted))
                .Include(t => t.Translations.Where(tr => !tr.IsDeleted))
                .FirstOrDefaultAsync(t => t.Id == sourceToolboxTalkId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);

            if (source == null)
            {
                _logger.LogWarning("Source toolbox talk {SourceId} not found", sourceToolboxTalkId);
                return new ContentReuseResult
                {
                    Success = false,
                    ErrorMessage = "Source toolbox talk not found"
                };
            }

            // Load target toolbox talk
            var target = await _dbContext.ToolboxTalks
                .Include(t => t.Sections.Where(s => !s.IsDeleted))
                .Include(t => t.Questions.Where(q => !q.IsDeleted))
                .Include(t => t.Translations.Where(tr => !tr.IsDeleted))
                .FirstOrDefaultAsync(t => t.Id == targetToolboxTalkId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);

            if (target == null)
            {
                _logger.LogWarning("Target toolbox talk {TargetId} not found", targetToolboxTalkId);
                return new ContentReuseResult
                {
                    Success = false,
                    ErrorMessage = "Target toolbox talk not found"
                };
            }

            var currentTime = DateTime.UtcNow;
            var currentUser = _currentUser.UserId ?? "System";

            // Soft delete existing content on target
            foreach (var section in target.Sections.ToList())
            {
                section.IsDeleted = true;
                section.UpdatedAt = currentTime;
                section.UpdatedBy = currentUser;
            }

            foreach (var question in target.Questions.ToList())
            {
                question.IsDeleted = true;
                question.UpdatedAt = currentTime;
                question.UpdatedBy = currentUser;
            }

            foreach (var translation in target.Translations.ToList())
            {
                translation.IsDeleted = true;
                translation.UpdatedAt = currentTime;
                translation.UpdatedBy = currentUser;
            }

            var sectionsCopied = 0;
            var questionsCopied = 0;
            var translationsCopied = 0;

            // Build source→target ID maps for remapping translation JSON
            var sectionIdMap = new Dictionary<Guid, Guid>();
            var questionIdMap = new Dictionary<Guid, Guid>();

            // Copy sections
            foreach (var sourceSection in source.Sections.OrderBy(s => s.SectionNumber))
            {
                var newSection = new ToolboxTalkSection
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = targetToolboxTalkId,
                    SectionNumber = sourceSection.SectionNumber,
                    Title = sourceSection.Title,
                    Content = sourceSection.Content,
                    RequiresAcknowledgment = sourceSection.RequiresAcknowledgment,
                    Source = sourceSection.Source,
                    VideoTimestamp = sourceSection.VideoTimestamp,
                    CreatedAt = currentTime,
                    CreatedBy = currentUser
                };

                sectionIdMap[sourceSection.Id] = newSection.Id;
                _dbContext.ToolboxTalkSections.Add(newSection);
                sectionsCopied++;
            }

            // Copy questions
            foreach (var sourceQuestion in source.Questions.OrderBy(q => q.QuestionNumber))
            {
                var newQuestion = new ToolboxTalkQuestion
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = targetToolboxTalkId,
                    QuestionNumber = sourceQuestion.QuestionNumber,
                    QuestionText = sourceQuestion.QuestionText,
                    QuestionType = sourceQuestion.QuestionType,
                    Options = sourceQuestion.Options,
                    CorrectAnswer = sourceQuestion.CorrectAnswer,
                    CorrectOptionIndex = sourceQuestion.CorrectOptionIndex,
                    Points = sourceQuestion.Points,
                    Source = sourceQuestion.Source,
                    IsFromVideoFinalPortion = sourceQuestion.IsFromVideoFinalPortion,
                    VideoTimestamp = sourceQuestion.VideoTimestamp,
                    CreatedAt = currentTime,
                    CreatedBy = currentUser
                };

                questionIdMap[sourceQuestion.Id] = newQuestion.Id;
                _dbContext.ToolboxTalkQuestions.Add(newQuestion);
                questionsCopied++;
            }

            // Copy translations with remapped section/question IDs
            foreach (var sourceTranslation in source.Translations)
            {
                var remappedSections = sectionIdMap.Count > 0
                    ? RemapTranslatedSections(sourceTranslation.TranslatedSections, sectionIdMap)
                    : sourceTranslation.TranslatedSections;

                var remappedQuestions = sourceTranslation.TranslatedQuestions != null && questionIdMap.Count > 0
                    ? RemapTranslatedQuestions(sourceTranslation.TranslatedQuestions, questionIdMap)
                    : sourceTranslation.TranslatedQuestions;

                var newTranslation = new ToolboxTalkTranslation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ToolboxTalkId = targetToolboxTalkId,
                    LanguageCode = sourceTranslation.LanguageCode,
                    TranslatedTitle = sourceTranslation.TranslatedTitle,
                    TranslatedDescription = sourceTranslation.TranslatedDescription,
                    TranslatedSections = remappedSections,
                    TranslatedQuestions = remappedQuestions,
                    EmailSubject = sourceTranslation.EmailSubject,
                    EmailBody = sourceTranslation.EmailBody,
                    TranslatedAt = currentTime,
                    TranslationProvider = "ContentReuse",
                    CreatedAt = currentTime,
                    CreatedBy = currentUser
                };

                _dbContext.ToolboxTalkTranslations.Add(newTranslation);
                translationsCopied++;
            }

            // Copy HTML slideshow if source has it
            var slideshowCopied = false;
            if (!string.IsNullOrEmpty(source.SlideshowHtml))
            {
                target.SlideshowHtml = source.SlideshowHtml;
                target.SlideshowGeneratedAt = currentTime;
                // SlidesGenerated is only set when actual ToolboxTalkSlide records exist (below)
                slideshowCopied = true;

                // Copy slideshow translations
                var slideshowTranslations = await _dbContext.ToolboxTalkSlideshowTranslations
                    .Where(t => t.ToolboxTalkId == sourceToolboxTalkId)
                    .ToListAsync(cancellationToken);

                foreach (var slideshowTranslation in slideshowTranslations)
                {
                    _dbContext.ToolboxTalkSlideshowTranslations.Add(new ToolboxTalkSlideshowTranslation
                    {
                        ToolboxTalkId = targetToolboxTalkId,
                        LanguageCode = slideshowTranslation.LanguageCode,
                        TranslatedHtml = slideshowTranslation.TranslatedHtml,
                        TranslatedAt = currentTime
                    });
                }
            }

            // Copy individual slide records (PDF page images and their translations)
            var sourceSlides = await _dbContext.ToolboxTalkSlides
                .Include(s => s.Translations)
                .Where(s => s.ToolboxTalkId == sourceToolboxTalkId && !s.IsDeleted)
                .OrderBy(s => s.PageNumber)
                .ToListAsync(cancellationToken);

            if (sourceSlides.Count > 0)
            {
                // Soft delete existing slides on target
                var existingTargetSlides = await _dbContext.ToolboxTalkSlides
                    .Where(s => s.ToolboxTalkId == targetToolboxTalkId && !s.IsDeleted)
                    .ToListAsync(cancellationToken);
                foreach (var existingSlide in existingTargetSlides)
                {
                    existingSlide.IsDeleted = true;
                    existingSlide.UpdatedAt = currentTime;
                    existingSlide.UpdatedBy = currentUser;
                }

                foreach (var sourceSlide in sourceSlides)
                {
                    var newSlide = new ToolboxTalkSlide
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ToolboxTalkId = targetToolboxTalkId,
                        PageNumber = sourceSlide.PageNumber,
                        ImageStoragePath = sourceSlide.ImageStoragePath,
                        OriginalText = sourceSlide.OriginalText,
                        CreatedAt = currentTime,
                        CreatedBy = currentUser
                    };

                    _dbContext.ToolboxTalkSlides.Add(newSlide);

                    foreach (var slideTranslation in sourceSlide.Translations)
                    {
                        _dbContext.ToolboxTalkSlideTranslations.Add(new ToolboxTalkSlideTranslation
                        {
                            Id = Guid.NewGuid(),
                            SlideId = newSlide.Id,
                            LanguageCode = slideTranslation.LanguageCode,
                            TranslatedText = slideTranslation.TranslatedText
                        });
                    }
                }

                // Ensure SlidesGenerated is set even if there was no SlideshowHtml
                target.SlidesGenerated = true;

                _logger.LogInformation(
                    "Copied {SlideCount} slide(s) with translations from source talk {SourceId} to target {TargetId}",
                    sourceSlides.Count, sourceToolboxTalkId, targetToolboxTalkId);
            }

            // Copy subtitle data from the source's latest completed processing job
            var subtitlesCopied = false;
            var sourceSubtitleJob = await _dbContext.SubtitleProcessingJobs
                .Include(j => j.Translations)
                .Where(j => j.ToolboxTalkId == sourceToolboxTalkId
                    && !j.IsDeleted
                    && j.Status == SubtitleProcessingStatus.Completed)
                .OrderByDescending(j => j.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceSubtitleJob != null && !string.IsNullOrEmpty(sourceSubtitleJob.EnglishSrtContent))
            {
                var existingTargetJobs = await _dbContext.SubtitleProcessingJobs
                    .Where(j => j.ToolboxTalkId == targetToolboxTalkId && !j.IsDeleted)
                    .ToListAsync(cancellationToken);
                foreach (var existingJob in existingTargetJobs)
                {
                    existingJob.IsDeleted = true;
                    existingJob.UpdatedAt = currentTime;
                    existingJob.UpdatedBy = currentUser;
                }

                var newSubtitleJob = new SubtitleProcessingJob
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ToolboxTalkId = targetToolboxTalkId,
                    Status = SubtitleProcessingStatus.Completed,
                    SourceVideoUrl = sourceSubtitleJob.SourceVideoUrl,
                    VideoSourceType = sourceSubtitleJob.VideoSourceType,
                    StartedAt = currentTime,
                    CompletedAt = currentTime,
                    TotalSubtitles = sourceSubtitleJob.TotalSubtitles,
                    EnglishSrtContent = sourceSubtitleJob.EnglishSrtContent,
                    EnglishSrtUrl = sourceSubtitleJob.EnglishSrtUrl,
                    CreatedAt = currentTime,
                    CreatedBy = currentUser
                };

                _dbContext.SubtitleProcessingJobs.Add(newSubtitleJob);

                foreach (var sourceSubTranslation in sourceSubtitleJob.Translations
                    .Where(t => t.Status == SubtitleTranslationStatus.Completed))
                {
                    _dbContext.SubtitleTranslations.Add(new SubtitleTranslation
                    {
                        SubtitleProcessingJobId = newSubtitleJob.Id,
                        Language = sourceSubTranslation.Language,
                        LanguageCode = sourceSubTranslation.LanguageCode,
                        Status = SubtitleTranslationStatus.Completed,
                        SubtitlesProcessed = sourceSubTranslation.SubtitlesProcessed,
                        TotalSubtitles = sourceSubTranslation.TotalSubtitles,
                        SrtContent = sourceSubTranslation.SrtContent,
                        SrtUrl = sourceSubTranslation.SrtUrl
                    });
                }

                subtitlesCopied = true;
                _logger.LogInformation(
                    "Copied subtitle data from source job {SourceJobId} to target talk {TargetId}",
                    sourceSubtitleJob.Id, targetToolboxTalkId);
            }

            // Copy file hash and URL (reuse the same blob)
            if (!string.IsNullOrEmpty(source.PdfFileHash))
            {
                target.PdfFileHash = source.PdfFileHash;
                target.PdfUrl = source.PdfUrl;
                target.PdfFileName = source.PdfFileName;
                target.ExtractedPdfText = source.ExtractedPdfText;
                target.PdfTextExtractedAt = source.PdfTextExtractedAt;
                target.GeneratedFromPdf = source.GeneratedFromPdf;
            }

            if (!string.IsNullOrEmpty(source.VideoFileHash))
            {
                target.VideoFileHash = source.VideoFileHash;
                target.VideoUrl = source.VideoUrl;
                target.VideoSource = source.VideoSource;
                target.ExtractedVideoTranscript = source.ExtractedVideoTranscript;
                target.VideoTranscriptExtractedAt = source.VideoTranscriptExtractedAt;
                target.GeneratedFromVideo = source.GeneratedFromVideo;
            }

            // Update target metadata
            target.ContentGeneratedAt = currentTime;
            target.RequiresQuiz = questionsCopied > 0;
            target.Status = ToolboxTalkStatus.ReadyForReview;
            target.UpdatedAt = currentTime;
            target.UpdatedBy = currentUser;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Content reuse complete. Sections: {Sections}, Questions: {Questions}, " +
                "Translations: {Translations}, Slideshow: {Slideshow}, Subtitles: {Subtitles}",
                sectionsCopied, questionsCopied, translationsCopied, slideshowCopied, subtitlesCopied);

            return new ContentReuseResult
            {
                Success = true,
                SectionsCopied = sectionsCopied,
                QuestionsCopied = questionsCopied,
                SlideshowCopied = slideshowCopied,
                TranslationsCopied = translationsCopied,
                SubtitlesCopied = subtitlesCopied
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during content reuse. TargetId: {TargetId}, SourceId: {SourceId}",
                targetToolboxTalkId, sourceToolboxTalkId);

            return new ContentReuseResult
            {
                Success = false,
                ErrorMessage = $"Failed to reuse content: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task<ContentReuseResult> ReuseContentAsync(
        Guid targetToolboxTalkId,
        Guid sourceToolboxTalkId,
        Guid tenantId,
        ReuseContentOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting selective content reuse. TargetId: {TargetId}, SourceId: {SourceId}, TenantId: {TenantId}, " +
            "CopySections: {CopySections}, CopyQuestions: {CopyQuestions}, CopySlideshow: {CopySlideshow}, CopyTranslations: {CopyTranslations}",
            targetToolboxTalkId, sourceToolboxTalkId, tenantId,
            options.CopySections, options.CopyQuestions, options.CopySlideshow, options.CopyTranslations);

        try
        {
            // Load source toolbox talk with all content
            var source = await _dbContext.ToolboxTalks
                .Include(t => t.Sections.Where(s => !s.IsDeleted))
                .Include(t => t.Questions.Where(q => !q.IsDeleted))
                .Include(t => t.Translations.Where(tr => !tr.IsDeleted))
                .FirstOrDefaultAsync(t => t.Id == sourceToolboxTalkId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);

            if (source == null)
            {
                _logger.LogWarning("Source toolbox talk {SourceId} not found", sourceToolboxTalkId);
                return new ContentReuseResult
                {
                    Success = false,
                    ErrorMessage = "Source toolbox talk not found"
                };
            }

            // Load target toolbox talk
            var target = await _dbContext.ToolboxTalks
                .Include(t => t.Sections.Where(s => !s.IsDeleted))
                .Include(t => t.Questions.Where(q => !q.IsDeleted))
                .Include(t => t.Translations.Where(tr => !tr.IsDeleted))
                .FirstOrDefaultAsync(t => t.Id == targetToolboxTalkId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);

            if (target == null)
            {
                _logger.LogWarning("Target toolbox talk {TargetId} not found", targetToolboxTalkId);
                return new ContentReuseResult
                {
                    Success = false,
                    ErrorMessage = "Target toolbox talk not found"
                };
            }

            var currentTime = DateTime.UtcNow;
            var currentUser = _currentUser.UserId ?? "System";

            var sectionsCopied = 0;
            var questionsCopied = 0;
            var slideshowCopied = false;
            var translationsCopied = 0;

            // Build source→target ID maps for remapping translation JSON
            var sectionIdMap = new Dictionary<Guid, Guid>();
            var questionIdMap = new Dictionary<Guid, Guid>();

            // Copy sections if requested
            if (options.CopySections && source.Sections.Count > 0)
            {
                // Soft delete existing sections on target
                foreach (var section in target.Sections.ToList())
                {
                    section.IsDeleted = true;
                    section.UpdatedAt = currentTime;
                    section.UpdatedBy = currentUser;
                }

                foreach (var sourceSection in source.Sections.OrderBy(s => s.SectionNumber))
                {
                    var newSection = new ToolboxTalkSection
                    {
                        Id = Guid.NewGuid(),
                        ToolboxTalkId = targetToolboxTalkId,
                        SectionNumber = sourceSection.SectionNumber,
                        Title = sourceSection.Title,
                        Content = sourceSection.Content,
                        RequiresAcknowledgment = sourceSection.RequiresAcknowledgment,
                        Source = sourceSection.Source,
                        VideoTimestamp = sourceSection.VideoTimestamp,
                        CreatedAt = currentTime,
                        CreatedBy = currentUser
                    };

                    sectionIdMap[sourceSection.Id] = newSection.Id;
                    _dbContext.ToolboxTalkSections.Add(newSection);
                    sectionsCopied++;
                }
            }

            // Copy questions if requested
            if (options.CopyQuestions && source.Questions.Count > 0)
            {
                // Soft delete existing questions on target
                foreach (var question in target.Questions.ToList())
                {
                    question.IsDeleted = true;
                    question.UpdatedAt = currentTime;
                    question.UpdatedBy = currentUser;
                }

                foreach (var sourceQuestion in source.Questions.OrderBy(q => q.QuestionNumber))
                {
                    var newQuestion = new ToolboxTalkQuestion
                    {
                        Id = Guid.NewGuid(),
                        ToolboxTalkId = targetToolboxTalkId,
                        QuestionNumber = sourceQuestion.QuestionNumber,
                        QuestionText = sourceQuestion.QuestionText,
                        QuestionType = sourceQuestion.QuestionType,
                        Options = sourceQuestion.Options,
                        CorrectAnswer = sourceQuestion.CorrectAnswer,
                        CorrectOptionIndex = sourceQuestion.CorrectOptionIndex,
                        Points = sourceQuestion.Points,
                        Source = sourceQuestion.Source,
                        IsFromVideoFinalPortion = sourceQuestion.IsFromVideoFinalPortion,
                        VideoTimestamp = sourceQuestion.VideoTimestamp,
                        CreatedAt = currentTime,
                        CreatedBy = currentUser
                    };

                    questionIdMap[sourceQuestion.Id] = newQuestion.Id;
                    _dbContext.ToolboxTalkQuestions.Add(newQuestion);
                    questionsCopied++;
                }
            }

            // Copy HTML slideshow if requested and source has it
            if (options.CopySlideshow && !string.IsNullOrEmpty(source.SlideshowHtml))
            {
                target.SlideshowHtml = source.SlideshowHtml;
                target.SlideshowGeneratedAt = currentTime;
                // SlidesGenerated is only set when actual ToolboxTalkSlide records exist (below)
                slideshowCopied = true;

                // Copy slideshow translations
                var slideshowTranslations = await _dbContext.ToolboxTalkSlideshowTranslations
                    .Where(t => t.ToolboxTalkId == sourceToolboxTalkId)
                    .ToListAsync(cancellationToken);

                foreach (var translation in slideshowTranslations)
                {
                    _dbContext.ToolboxTalkSlideshowTranslations.Add(new ToolboxTalkSlideshowTranslation
                    {
                        ToolboxTalkId = targetToolboxTalkId,
                        LanguageCode = translation.LanguageCode,
                        TranslatedHtml = translation.TranslatedHtml,
                        TranslatedAt = currentTime
                    });
                }
            }

            // Copy individual slide records if slideshow was copied
            if (options.CopySlideshow)
            {
                var sourceSlides = await _dbContext.ToolboxTalkSlides
                    .Include(s => s.Translations)
                    .Where(s => s.ToolboxTalkId == sourceToolboxTalkId && !s.IsDeleted)
                    .OrderBy(s => s.PageNumber)
                    .ToListAsync(cancellationToken);

                if (sourceSlides.Count > 0)
                {
                    // Soft delete existing slides on target
                    var existingTargetSlides = await _dbContext.ToolboxTalkSlides
                        .Where(s => s.ToolboxTalkId == targetToolboxTalkId && !s.IsDeleted)
                        .ToListAsync(cancellationToken);
                    foreach (var existingSlide in existingTargetSlides)
                    {
                        existingSlide.IsDeleted = true;
                        existingSlide.UpdatedAt = currentTime;
                        existingSlide.UpdatedBy = currentUser;
                    }

                    foreach (var sourceSlide in sourceSlides)
                    {
                        var newSlide = new ToolboxTalkSlide
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            ToolboxTalkId = targetToolboxTalkId,
                            PageNumber = sourceSlide.PageNumber,
                            ImageStoragePath = sourceSlide.ImageStoragePath,
                            OriginalText = sourceSlide.OriginalText,
                            CreatedAt = currentTime,
                            CreatedBy = currentUser
                        };

                        _dbContext.ToolboxTalkSlides.Add(newSlide);

                        foreach (var slideTranslation in sourceSlide.Translations)
                        {
                            _dbContext.ToolboxTalkSlideTranslations.Add(new ToolboxTalkSlideTranslation
                            {
                                Id = Guid.NewGuid(),
                                SlideId = newSlide.Id,
                                LanguageCode = slideTranslation.LanguageCode,
                                TranslatedText = slideTranslation.TranslatedText
                            });
                        }
                    }

                    // Ensure SlidesGenerated is set even if there was no SlideshowHtml
                    target.SlidesGenerated = true;

                    _logger.LogInformation(
                        "Copied {SlideCount} slide(s) with translations from source talk {SourceId} to target {TargetId}",
                        sourceSlides.Count, sourceToolboxTalkId, targetToolboxTalkId);
                }
            }

            // Copy translations if requested, remapping section/question IDs when those were also copied
            if (options.CopyTranslations && source.Translations.Count > 0)
            {
                // Soft delete existing translations on target
                foreach (var translation in target.Translations.ToList())
                {
                    translation.IsDeleted = true;
                    translation.UpdatedAt = currentTime;
                    translation.UpdatedBy = currentUser;
                }

                foreach (var sourceTranslation in source.Translations)
                {
                    // Only remap IDs for content types that were actually copied
                    var remappedSections = options.CopySections && sectionIdMap.Count > 0
                        ? RemapTranslatedSections(sourceTranslation.TranslatedSections, sectionIdMap)
                        : sourceTranslation.TranslatedSections;

                    var remappedQuestions = options.CopyQuestions && sourceTranslation.TranslatedQuestions != null && questionIdMap.Count > 0
                        ? RemapTranslatedQuestions(sourceTranslation.TranslatedQuestions, questionIdMap)
                        : sourceTranslation.TranslatedQuestions;

                    var newTranslation = new ToolboxTalkTranslation
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        ToolboxTalkId = targetToolboxTalkId,
                        LanguageCode = sourceTranslation.LanguageCode,
                        TranslatedTitle = sourceTranslation.TranslatedTitle,
                        TranslatedDescription = sourceTranslation.TranslatedDescription,
                        TranslatedSections = remappedSections,
                        TranslatedQuestions = remappedQuestions,
                        EmailSubject = sourceTranslation.EmailSubject,
                        EmailBody = sourceTranslation.EmailBody,
                        TranslatedAt = currentTime,
                        TranslationProvider = "ContentReuse",
                        CreatedAt = currentTime,
                        CreatedBy = currentUser
                    };

                    _dbContext.ToolboxTalkTranslations.Add(newTranslation);
                    translationsCopied++;
                }
            }

            // Copy subtitle data from the source's latest completed processing job
            var subtitlesCopied = false;
            var sourceSubtitleJob = await _dbContext.SubtitleProcessingJobs
                .Include(j => j.Translations)
                .Where(j => j.ToolboxTalkId == sourceToolboxTalkId
                    && !j.IsDeleted
                    && j.Status == Domain.Enums.SubtitleProcessingStatus.Completed)
                .OrderByDescending(j => j.CompletedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (sourceSubtitleJob != null && !string.IsNullOrEmpty(sourceSubtitleJob.EnglishSrtContent))
            {
                // Soft delete any existing subtitle jobs on the target
                var existingTargetJobs = await _dbContext.SubtitleProcessingJobs
                    .Where(j => j.ToolboxTalkId == targetToolboxTalkId && !j.IsDeleted)
                    .ToListAsync(cancellationToken);
                foreach (var existingJob in existingTargetJobs)
                {
                    existingJob.IsDeleted = true;
                    existingJob.UpdatedAt = currentTime;
                    existingJob.UpdatedBy = currentUser;
                }

                // Create a copy of the subtitle processing job for the target talk
                var newSubtitleJob = new SubtitleProcessingJob
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ToolboxTalkId = targetToolboxTalkId,
                    Status = Domain.Enums.SubtitleProcessingStatus.Completed,
                    SourceVideoUrl = sourceSubtitleJob.SourceVideoUrl,
                    VideoSourceType = sourceSubtitleJob.VideoSourceType,
                    StartedAt = currentTime,
                    CompletedAt = currentTime,
                    TotalSubtitles = sourceSubtitleJob.TotalSubtitles,
                    EnglishSrtContent = sourceSubtitleJob.EnglishSrtContent,
                    EnglishSrtUrl = sourceSubtitleJob.EnglishSrtUrl,
                    CreatedAt = currentTime,
                    CreatedBy = currentUser
                };

                _dbContext.SubtitleProcessingJobs.Add(newSubtitleJob);

                // Copy all completed subtitle translations
                foreach (var sourceTranslation in sourceSubtitleJob.Translations
                    .Where(t => t.Status == Domain.Enums.SubtitleTranslationStatus.Completed))
                {
                    _dbContext.SubtitleTranslations.Add(new SubtitleTranslation
                    {
                        SubtitleProcessingJobId = newSubtitleJob.Id,
                        Language = sourceTranslation.Language,
                        LanguageCode = sourceTranslation.LanguageCode,
                        Status = Domain.Enums.SubtitleTranslationStatus.Completed,
                        SubtitlesProcessed = sourceTranslation.SubtitlesProcessed,
                        TotalSubtitles = sourceTranslation.TotalSubtitles,
                        SrtContent = sourceTranslation.SrtContent,
                        SrtUrl = sourceTranslation.SrtUrl
                    });
                }

                subtitlesCopied = true;
                _logger.LogInformation(
                    "Copied subtitle data from source job {SourceJobId} ({TranslationCount} translations) to target talk {TargetId}",
                    sourceSubtitleJob.Id, sourceSubtitleJob.Translations.Count(t => t.Status == Domain.Enums.SubtitleTranslationStatus.Completed),
                    targetToolboxTalkId);
            }

            // Copy file hash and URL (reuse the same blob)
            if (!string.IsNullOrEmpty(source.PdfFileHash))
            {
                target.PdfFileHash = source.PdfFileHash;
                target.ExtractedPdfText = source.ExtractedPdfText;
                target.PdfTextExtractedAt = source.PdfTextExtractedAt;
                target.GeneratedFromPdf = source.GeneratedFromPdf;
            }

            if (!string.IsNullOrEmpty(source.VideoFileHash))
            {
                target.VideoFileHash = source.VideoFileHash;
                target.ExtractedVideoTranscript = source.ExtractedVideoTranscript;
                target.VideoTranscriptExtractedAt = source.VideoTranscriptExtractedAt;
                target.GeneratedFromVideo = source.GeneratedFromVideo;
            }

            // Update target metadata
            target.ContentGeneratedAt = currentTime;
            target.RequiresQuiz = questionsCopied > 0 || target.Questions.Any(q => !q.IsDeleted);
            target.Status = ToolboxTalkStatus.ReadyForReview;
            target.UpdatedAt = currentTime;
            target.UpdatedBy = currentUser;

            var saved = await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Selective content reuse complete. Sections: {Sections}, Questions: {Questions}, " +
                "Slideshow: {Slideshow}, Translations: {Translations}, Subtitles: {Subtitles}, RowsSaved: {Saved}",
                sectionsCopied, questionsCopied, slideshowCopied, translationsCopied, subtitlesCopied, saved);

            return new ContentReuseResult
            {
                Success = true,
                SectionsCopied = sectionsCopied,
                QuestionsCopied = questionsCopied,
                SlideshowCopied = slideshowCopied,
                TranslationsCopied = translationsCopied,
                SubtitlesCopied = subtitlesCopied
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during selective content reuse. TargetId: {TargetId}, SourceId: {SourceId}",
                targetToolboxTalkId, sourceToolboxTalkId);

            return new ContentReuseResult
            {
                Success = false,
                ErrorMessage = $"Failed to reuse content: {ex.Message}"
            };
        }
    }

    /// <inheritdoc />
    public async Task UpdateFileHashAsync(
        Guid toolboxTalkId,
        string fileHash,
        FileHashType fileType,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Updating file hash. ToolboxTalkId: {Id}, FileType: {Type}, Hash: {Hash}",
            toolboxTalkId, fileType, fileHash);

        var toolboxTalk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId && t.TenantId == tenantId && !t.IsDeleted, cancellationToken);

        if (toolboxTalk == null)
        {
            _logger.LogWarning("Toolbox talk {Id} not found for hash update", toolboxTalkId);
            return;
        }

        if (fileType == FileHashType.Pdf)
        {
            toolboxTalk.PdfFileHash = fileHash;
        }
        else
        {
            toolboxTalk.VideoFileHash = fileHash;
        }

        toolboxTalk.UpdatedAt = DateTime.UtcNow;
        toolboxTalk.UpdatedBy = _currentUser.UserId ?? "System";

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Remaps SectionId values in the TranslatedSections JSON from source IDs to target IDs.
    /// JSON format: [{SectionId, Title, Content}, ...]
    /// </summary>
    private string RemapTranslatedSections(string json, Dictionary<Guid, Guid> sectionIdMap)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var sections = new List<Dictionary<string, object>>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var section = new Dictionary<string, object>();

                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name.Equals("SectionId", StringComparison.OrdinalIgnoreCase)
                        && prop.Value.TryGetGuid(out var oldId)
                        && sectionIdMap.TryGetValue(oldId, out var newId))
                    {
                        section[prop.Name] = newId;
                    }
                    else
                    {
                        section[prop.Name] = prop.Value.Clone();
                    }
                }

                sections.Add(section);
            }

            return JsonSerializer.Serialize(sections);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remap section IDs in translation JSON. Returning original.");
            return json;
        }
    }

    /// <summary>
    /// Remaps QuestionId values in the TranslatedQuestions JSON from source IDs to target IDs.
    /// JSON format: [{QuestionId, QuestionText, Options}, ...]
    /// </summary>
    private string RemapTranslatedQuestions(string json, Dictionary<Guid, Guid> questionIdMap)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var questions = new List<Dictionary<string, object>>();

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var question = new Dictionary<string, object>();

                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name.Equals("QuestionId", StringComparison.OrdinalIgnoreCase)
                        && prop.Value.TryGetGuid(out var oldId)
                        && questionIdMap.TryGetValue(oldId, out var newId))
                    {
                        question[prop.Name] = newId;
                    }
                    else
                    {
                        question[prop.Name] = prop.Value.Clone();
                    }
                }

                questions.Add(question);
            }

            return JsonSerializer.Serialize(questions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remap question IDs in translation JSON. Returning original.");
            return json;
        }
    }

    /// <summary>
    /// Gets the display name for a language code
    /// </summary>
    private static string GetLanguageName(string languageCode)
    {
        return languageCode.ToLowerInvariant() switch
        {
            "pl" => "Polish",
            "ro" => "Romanian",
            "pt" => "Portuguese",
            "es" => "Spanish",
            "fr" => "French",
            "de" => "German",
            "it" => "Italian",
            "ru" => "Russian",
            "uk" => "Ukrainian",
            "lt" => "Lithuanian",
            "lv" => "Latvian",
            "bg" => "Bulgarian",
            "hu" => "Hungarian",
            "cs" => "Czech",
            "sk" => "Slovak",
            "hr" => "Croatian",
            _ => languageCode.ToUpperInvariant()
        };
    }
}
