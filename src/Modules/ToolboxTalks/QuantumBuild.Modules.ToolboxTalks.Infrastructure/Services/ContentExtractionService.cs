using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Pdf;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Service that orchestrates the extraction of content from both video transcripts and PDFs,
/// preparing the combined content for AI generation of toolbox talk sections and quiz questions.
/// </summary>
public class ContentExtractionService : IContentExtractionService
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;
    private readonly IPdfExtractionService _pdfExtraction;
    private readonly ITranscriptService _transcriptService;
    private readonly ITranscriptionService _transcriptionService;
    private readonly ISrtGeneratorService _srtGeneratorService;
    private readonly ISrtStorageProvider _srtStorageProvider;
    private readonly IVideoSourceProvider _videoSourceProvider;
    private readonly ILanguageCodeService _languageCodeService;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ContentExtractionService> _logger;

    public ContentExtractionService(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext,
        IPdfExtractionService pdfExtraction,
        ITranscriptService transcriptService,
        ITranscriptionService transcriptionService,
        ISrtGeneratorService srtGeneratorService,
        ISrtStorageProvider srtStorageProvider,
        IVideoSourceProvider videoSourceProvider,
        ILanguageCodeService languageCodeService,
        IBackgroundJobClient backgroundJobClient,
        IOptions<SubtitleProcessingSettings> settings,
        ICurrentUserService currentUser,
        ILogger<ContentExtractionService> logger)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
        _pdfExtraction = pdfExtraction;
        _transcriptService = transcriptService;
        _transcriptionService = transcriptionService;
        _srtGeneratorService = srtGeneratorService;
        _srtStorageProvider = srtStorageProvider;
        _videoSourceProvider = videoSourceProvider;
        _languageCodeService = languageCodeService;
        _backgroundJobClient = backgroundJobClient;
        _settings = settings.Value;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ContentExtractionResult> ExtractContentAsync(
        Guid toolboxTalkId,
        bool includeVideo,
        bool includePdf,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[ContentExtractionService] Starting extraction for toolbox talk {Id}. TenantId={TenantId}, IncludeVideo={IncludeVideo}, IncludePdf={IncludePdf}",
            toolboxTalkId, tenantId, includeVideo, includePdf);

        var errors = new List<string>();
        var warnings = new List<string>();
        var sourceFailures = new List<string>(); // Track which sources failed
        VideoContentInfo? videoContent = null;
        PdfContentInfo? pdfContent = null;

        // Get the toolbox talk from database
        var toolboxTalk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId && t.TenantId == tenantId, cancellationToken);

        if (toolboxTalk == null)
        {
            _logger.LogWarning("[ContentExtractionService] Toolbox talk {Id} not found for tenant {TenantId}",
                toolboxTalkId, tenantId);
            return new ContentExtractionResult(
                Success: false,
                PartialSuccess: false,
                CombinedContent: null,
                VideoContent: null,
                PdfContent: null,
                Errors: new List<string> { "Learning not found" },
                Warnings: new List<string>());
        }

        _logger.LogDebug(
            "[ContentExtractionService] Found toolbox talk: Title='{Title}', VideoUrl='{VideoUrl}', PdfUrl='{PdfUrl}'",
            toolboxTalk.Title,
            string.IsNullOrEmpty(toolboxTalk.VideoUrl) ? "(none)" : toolboxTalk.VideoUrl,
            string.IsNullOrEmpty(toolboxTalk.PdfUrl) ? "(none)" : toolboxTalk.PdfUrl);

        // Extract video transcript if requested
        if (includeVideo)
        {
            if (string.IsNullOrEmpty(toolboxTalk.VideoUrl))
            {
                _logger.LogWarning(
                    "[Video Extraction] SKIPPED - No video URL set for toolbox talk {Id}",
                    toolboxTalkId);
                sourceFailures.Add("Video extraction requested but no video URL is set");
            }
            else
            {
                _logger.LogInformation(
                    "[Video Extraction] Starting transcript extraction for toolbox talk {Id}. VideoUrl: {VideoUrl}",
                    toolboxTalkId, toolboxTalk.VideoUrl);

                try
                {
                    // First, try to get existing transcript
                    var transcriptResult = await _transcriptService.GetTranscriptAsync(
                        toolboxTalkId,
                        totalVideoDuration: null,
                        cancellationToken);

                    if (transcriptResult.Success && !string.IsNullOrEmpty(transcriptResult.FullText))
                    {
                        _logger.LogInformation(
                            "[Video Extraction] Found existing transcript for toolbox talk {Id}. Length: {Length} chars",
                            toolboxTalkId, transcriptResult.FullText.Length);

                        var finalPortionText = _transcriptService.GetTextForPercentageRange(
                            transcriptResult, 80, 100);

                        videoContent = new VideoContentInfo(
                            FullTranscript: transcriptResult.FullText,
                            FinalPortionTranscript: finalPortionText,
                            Duration: transcriptResult.TotalDuration ?? TimeSpan.Zero,
                            SegmentCount: transcriptResult.Segments.Count);

                        // Cache the extracted transcript in the entity
                        toolboxTalk.ExtractedVideoTranscript = transcriptResult.FullText;
                        toolboxTalk.VideoTranscriptExtractedAt = DateTime.UtcNow;

                        _logger.LogInformation(
                            "[Video Extraction] SUCCESS - Extracted transcript: {SegmentCount} segments, " +
                            "duration {Duration}, fullTextLength={FullLength}, finalPortionLength={FinalLength}",
                            transcriptResult.Segments.Count,
                            transcriptResult.TotalDuration,
                            transcriptResult.FullText.Length,
                            finalPortionText?.Length ?? 0);
                    }
                    else
                    {
                        // No existing transcript - auto-generate via ElevenLabs
                        _logger.LogInformation(
                            "[Video Extraction] No existing transcript found. Starting auto-transcription via ElevenLabs for toolbox talk {Id}",
                            toolboxTalkId);

                        var autoTranscribeResult = await AutoTranscribeVideoAsync(
                            toolboxTalk, tenantId, cancellationToken);

                        if (autoTranscribeResult.Success && autoTranscribeResult.VideoContent != null)
                        {
                            videoContent = autoTranscribeResult.VideoContent;
                        }
                        else
                        {
                            _logger.LogError(
                                "[Video Extraction] FAILED - Auto-transcription failed. Error: {Error}",
                                autoTranscribeResult.ErrorMessage);
                            sourceFailures.Add(GetUserFriendlyVideoError(autoTranscribeResult.ErrorMessage));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[Video Extraction] EXCEPTION - Error extracting transcript for toolbox talk {Id}. " +
                        "ExceptionType: {ExceptionType}, Message: {Message}",
                        toolboxTalkId, ex.GetType().FullName, ex.Message);
                    sourceFailures.Add(GetUserFriendlyVideoError(ex.Message));
                }
            }
        }
        else
        {
            _logger.LogDebug("[Video Extraction] SKIPPED - Video extraction not requested");
        }

        // Extract PDF content if requested
        if (includePdf)
        {
            if (string.IsNullOrEmpty(toolboxTalk.PdfUrl))
            {
                _logger.LogWarning(
                    "[PDF Extraction] SKIPPED - No PDF URL set for toolbox talk {Id}",
                    toolboxTalkId);
                sourceFailures.Add("PDF extraction requested but no PDF document has been uploaded");
            }
            else
            {
                _logger.LogInformation(
                    "[PDF Extraction] Starting text extraction for toolbox talk {Id}. PdfUrl: {PdfUrl}",
                    toolboxTalkId, toolboxTalk.PdfUrl);

                try
                {
                    var pdfResult = await _pdfExtraction.ExtractTextFromUrlAsync(
                        toolboxTalk.PdfUrl,
                        cancellationToken);

                    if (pdfResult.Success && !string.IsNullOrEmpty(pdfResult.Text))
                    {
                        pdfContent = new PdfContentInfo(
                            FullText: pdfResult.Text,
                            PageCount: pdfResult.PageCount);

                        // Cache the extracted text in the entity
                        toolboxTalk.ExtractedPdfText = pdfResult.Text;
                        toolboxTalk.PdfTextExtractedAt = DateTime.UtcNow;

                        _logger.LogInformation(
                            "[PDF Extraction] SUCCESS - Extracted text: {PageCount} pages, {CharCount} characters",
                            pdfResult.PageCount, pdfResult.Text.Length);

                        // Warn if text is suspiciously short (might be scanned)
                        if (pdfResult.Text.Length < 500 && pdfResult.PageCount > 1)
                        {
                            _logger.LogWarning(
                                "[PDF Extraction] WARNING - Text seems too short ({CharCount} chars) for {PageCount} pages. " +
                                "Document may be scanned/image-based.",
                                pdfResult.Text.Length, pdfResult.PageCount);
                            warnings.Add(
                                "PDF text extraction returned very little text. " +
                                "The document may be scanned/image-based and require OCR.");
                        }
                    }
                    else
                    {
                        _logger.LogError(
                            "[PDF Extraction] FAILED - Text extraction returned failure. Error: {Error}",
                            pdfResult.ErrorMessage);
                        sourceFailures.Add(GetUserFriendlyPdfError(pdfResult.ErrorMessage));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[PDF Extraction] EXCEPTION - Error extracting PDF for toolbox talk {Id}. " +
                        "ExceptionType: {ExceptionType}, Message: {Message}",
                        toolboxTalkId, ex.GetType().FullName, ex.Message);
                    sourceFailures.Add(GetUserFriendlyPdfError(ex.Message));
                }
            }
        }
        else
        {
            _logger.LogDebug("[PDF Extraction] SKIPPED - PDF extraction not requested");
        }

        // Save cached extractions to database if we extracted anything
        if (videoContent != null || pdfContent != null)
        {
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogDebug("Saved extracted content cache for toolbox talk {Id}", toolboxTalkId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save extracted content cache for toolbox talk {Id}", toolboxTalkId);
                // Don't fail the operation if caching fails - the extraction was successful
            }
        }

        // Build combined content for AI processing
        var combinedContent = BuildCombinedContent(videoContent, pdfContent);

        // Determine success status:
        // - Success: We have at least some combined content to work with
        // - PartialSuccess: Some sources failed but we still have content from others
        var hasContent = !string.IsNullOrEmpty(combinedContent);
        var hadSourceFailures = sourceFailures.Count > 0;
        var success = hasContent;
        var partialSuccess = hasContent && hadSourceFailures;

        // If we have content, source failures become warnings (content can still be generated)
        // If we have no content, source failures become critical errors
        if (hasContent && hadSourceFailures)
        {
            foreach (var failure in sourceFailures)
            {
                warnings.Add($"{failure} - content will be generated from available sources");
            }
        }
        else if (!hasContent)
        {
            errors.AddRange(sourceFailures);
            if (errors.Count == 0)
            {
                errors.Add("No content could be extracted from any source. Please check your video and PDF files.");
            }
        }

        _logger.LogInformation(
            "[ContentExtractionService] Extraction complete for toolbox talk {Id}. " +
            "Success={Success}, PartialSuccess={PartialSuccess}, HasVideo={HasVideo}, HasPdf={HasPdf}, " +
            "CombinedContentLength={ContentLength}, Errors={ErrorCount}, Warnings={WarningCount}",
            toolboxTalkId,
            success,
            partialSuccess,
            videoContent != null,
            pdfContent != null,
            combinedContent?.Length ?? 0,
            errors.Count,
            warnings.Count);

        if (errors.Count > 0)
        {
            _logger.LogWarning(
                "[ContentExtractionService] Extraction errors for toolbox talk {Id}: {Errors}",
                toolboxTalkId, string.Join("; ", errors));
        }

        if (partialSuccess)
        {
            _logger.LogWarning(
                "[ContentExtractionService] Partial extraction for toolbox talk {Id}. Some sources failed: {Failures}",
                toolboxTalkId, string.Join("; ", sourceFailures));
        }

        return new ContentExtractionResult(
            Success: success,
            PartialSuccess: partialSuccess,
            CombinedContent: combinedContent,
            VideoContent: videoContent,
            PdfContent: pdfContent,
            Errors: errors,
            Warnings: warnings);
    }

    /// <summary>
    /// Builds the combined content string from extracted video and PDF content.
    /// The format is designed for AI prompts to understand the structure and
    /// requirements for quiz question generation.
    /// </summary>
    private static string BuildCombinedContent(VideoContentInfo? video, PdfContentInfo? pdf)
    {
        var builder = new StringBuilder();

        if (video != null)
        {
            builder.AppendLine("=== VIDEO TRANSCRIPT ===");
            builder.AppendLine();
            builder.AppendLine(video.FullTranscript);
            builder.AppendLine();
            builder.AppendLine("=== VIDEO FINAL PORTION (80-100%) ===");
            builder.AppendLine("(At least one quiz question MUST come from this section)");
            builder.AppendLine();
            builder.AppendLine(video.FinalPortionTranscript);
            builder.AppendLine();
        }

        if (pdf != null)
        {
            builder.AppendLine("=== PDF DOCUMENT CONTENT ===");
            builder.AppendLine();
            builder.AppendLine(pdf.FullText);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>
    /// Automatically transcribes a video via ElevenLabs when no existing transcript is found.
    /// Creates SRT content, uploads it to storage, and creates a SubtitleProcessingJob record.
    /// </summary>
    private async Task<AutoTranscribeResult> AutoTranscribeVideoAsync(
        ToolboxTalk toolboxTalk,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Check if ElevenLabs API key is configured
        if (string.IsNullOrEmpty(_settings.ElevenLabs.ApiKey))
        {
            _logger.LogWarning(
                "[Auto-Transcription] ElevenLabs API key is not configured for toolbox talk {Id}",
                toolboxTalk.Id);
            return AutoTranscribeResult.Failure(
                "ElevenLabs API key is not configured. Please configure it in appsettings.json or generate subtitles manually.");
        }

        try
        {
            // Step 1: Determine video source type and get direct URL
            var sourceType = DetermineVideoSourceType(toolboxTalk.VideoUrl!);

            _logger.LogInformation(
                "[Auto-Transcription] Getting direct URL for video. SourceType: {SourceType}, Url: {Url}",
                sourceType, toolboxTalk.VideoUrl);

            var videoUrlResult = await _videoSourceProvider.GetDirectUrlAsync(
                toolboxTalk.VideoUrl!,
                sourceType,
                cancellationToken);

            if (!videoUrlResult.Success || string.IsNullOrEmpty(videoUrlResult.DirectUrl))
            {
                _logger.LogError(
                    "[Auto-Transcription] Failed to get direct video URL. Error: {Error}",
                    videoUrlResult.ErrorMessage);
                return AutoTranscribeResult.Failure(
                    $"Failed to get video URL: {videoUrlResult.ErrorMessage}");
            }

            // Step 2: Transcribe video via ElevenLabs
            _logger.LogInformation(
                "[Auto-Transcription] Starting ElevenLabs transcription for toolbox talk {Id}. This may take a few minutes...",
                toolboxTalk.Id);

            var transcriptionResult = await _transcriptionService.TranscribeAsync(
                videoUrlResult.DirectUrl,
                cancellationToken);

            if (!transcriptionResult.Success || transcriptionResult.Words.Count == 0)
            {
                _logger.LogError(
                    "[Auto-Transcription] ElevenLabs transcription failed for toolbox talk {Id}. Error: {Error}",
                    toolboxTalk.Id, transcriptionResult.ErrorMessage);
                return AutoTranscribeResult.Failure(
                    $"Transcription failed: {transcriptionResult.ErrorMessage}");
            }

            _logger.LogInformation(
                "[Auto-Transcription] ElevenLabs transcription successful for toolbox talk {Id}. Words: {WordCount}",
                toolboxTalk.Id, transcriptionResult.Words.Count);

            // Step 3: Generate SRT content from transcription
            var srtContent = _srtGeneratorService.GenerateSrt(
                transcriptionResult.Words,
                _settings.WordsPerSubtitle);

            var subtitleCount = _srtGeneratorService.CountSubtitleBlocks(srtContent);

            _logger.LogInformation(
                "[Auto-Transcription] Generated SRT with {SubtitleCount} subtitle blocks for toolbox talk {Id}",
                subtitleCount, toolboxTalk.Id);

            // Step 4: Upload SRT to R2 storage
            var videoSlug = GenerateVideoSlug(toolboxTalk.Title);
            var englishFileName = $"{videoSlug}_en.srt";

            _logger.LogInformation(
                "[Auto-Transcription] Attempting SRT upload to R2. FileName: {FileName}, TenantId: {TenantId}",
                englishFileName, tenantId);

            string? srtUrl = null;
            try
            {
                var uploadResult = await _srtStorageProvider.UploadSrtAsync(
                    srtContent,
                    englishFileName,
                    tenantId,
                    cancellationToken);

                if (uploadResult.Success)
                {
                    srtUrl = uploadResult.Url;
                    _logger.LogInformation(
                        "[Auto-Transcription] Uploaded English SRT to R2: {Url}",
                        srtUrl);
                }
                else
                {
                    _logger.LogError(
                        "[Auto-Transcription] Failed to upload SRT to R2. FileName: {FileName}, Error: {Error}",
                        englishFileName, uploadResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[Auto-Transcription] Exception uploading SRT to R2. FileName: {FileName}",
                    englishFileName);
            }

            // Step 5: Create SubtitleProcessingJob record
            var subtitleJob = new SubtitleProcessingJob
            {
                Id = Guid.NewGuid(),
                ToolboxTalkId = toolboxTalk.Id,
                TenantId = tenantId,
                Status = SubtitleProcessingStatus.Completed,
                SourceVideoUrl = toolboxTalk.VideoUrl!,
                VideoSourceType = sourceType,
                EnglishSrtContent = srtContent,
                EnglishSrtUrl = srtUrl,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                TotalSubtitles = subtitleCount
            };

            // Add English translation record
            subtitleJob.Translations.Add(new SubtitleTranslation
            {
                Id = Guid.NewGuid(),
                Language = "English",
                LanguageCode = "en",
                Status = SubtitleTranslationStatus.Completed,
                TotalSubtitles = subtitleCount,
                SubtitlesProcessed = subtitleCount,
                SrtContent = srtContent,
                SrtUrl = srtUrl
            });

            _dbContext.SubtitleProcessingJobs.Add(subtitleJob);

            // Step 6: Get target languages from employees and add translation records
            var targetLanguages = await GetTargetLanguagesFromEmployeesAsync(tenantId, cancellationToken);
            var hasTranslationsToProcess = false;

            foreach (var language in targetLanguages)
            {
                // Skip English as it's already done
                if (language.Equals("English", StringComparison.OrdinalIgnoreCase) ||
                    language.Equals("en", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var languageCode = await _languageCodeService.GetLanguageCodeAsync(language);
                var languageName = language.Length <= 3
                    ? await _languageCodeService.GetLanguageNameAsync(language)
                    : language;

                subtitleJob.Translations.Add(new SubtitleTranslation
                {
                    Id = Guid.NewGuid(),
                    Language = languageName,
                    LanguageCode = languageCode,
                    Status = SubtitleTranslationStatus.Pending,
                    TotalSubtitles = subtitleCount,
                    SubtitlesProcessed = 0
                });

                hasTranslationsToProcess = true;

                _logger.LogInformation(
                    "[Auto-Transcription] Queued translation to {Language} ({LanguageCode}) for toolbox talk {Id}",
                    languageName, languageCode, toolboxTalk.Id);
            }

            // Update job status to indicate translations are pending
            if (hasTranslationsToProcess)
            {
                subtitleJob.Status = SubtitleProcessingStatus.Translating;
            }

            // Step 7: Update toolbox talk with transcript data
            toolboxTalk.VideoTranscriptExtractedAt = DateTime.UtcNow;
            toolboxTalk.ExtractedVideoTranscript = srtContent;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Step 8: Queue background job to process translations
            if (hasTranslationsToProcess)
            {
                _backgroundJobClient.Enqueue<ISubtitleProcessingOrchestrator>(
                    orchestrator => orchestrator.ProcessRetryAsync(subtitleJob.Id, CancellationToken.None));

                _logger.LogInformation(
                    "[Auto-Transcription] Queued translation job for {Count} languages for toolbox talk {Id}",
                    targetLanguages.Count - 1, toolboxTalk.Id); // -1 to exclude English
            }

            _logger.LogInformation(
                "[Auto-Transcription] Auto-transcription complete for toolbox talk {Id}. SRT length: {Length} chars",
                toolboxTalk.Id, srtContent.Length);

            // Step 9: Parse the SRT to get structured transcript data
            var parsedTranscript = _transcriptService.ParseSrtContent(srtContent, null);

            if (!parsedTranscript.Success)
            {
                _logger.LogWarning(
                    "[Auto-Transcription] Failed to parse generated SRT. Using raw SRT content.");
            }

            var finalPortionText = parsedTranscript.Success
                ? _transcriptService.GetTextForPercentageRange(parsedTranscript, 80, 100)
                : null;

            var videoContent = new VideoContentInfo(
                FullTranscript: parsedTranscript.Success && !string.IsNullOrEmpty(parsedTranscript.FullText)
                    ? parsedTranscript.FullText
                    : _transcriptService.FormatForAi(parsedTranscript),
                FinalPortionTranscript: finalPortionText ?? string.Empty,
                Duration: parsedTranscript.TotalDuration ?? TimeSpan.Zero,
                SegmentCount: parsedTranscript.Segments.Count);

            return AutoTranscribeResult.SuccessResult(videoContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Auto-Transcription] Auto-transcription failed for toolbox talk {Id}",
                toolboxTalk.Id);
            return AutoTranscribeResult.Failure($"Auto-transcription failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines the video source type based on the URL.
    /// </summary>
    private static SubtitleVideoSourceType DetermineVideoSourceType(string videoUrl)
    {
        if (string.IsNullOrEmpty(videoUrl))
            return SubtitleVideoSourceType.DirectUrl;

        var lowerUrl = videoUrl.ToLowerInvariant();

        if (lowerUrl.Contains("drive.google.com") || lowerUrl.Contains("docs.google.com"))
            return SubtitleVideoSourceType.GoogleDrive;

        if (lowerUrl.Contains("blob.core.windows.net"))
            return SubtitleVideoSourceType.AzureBlob;

        return SubtitleVideoSourceType.DirectUrl;
    }

    /// <summary>
    /// Generates a URL-safe slug from a video title.
    /// </summary>
    private static string GenerateVideoSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "_");
        return slug.Trim('_');
    }

    /// <summary>
    /// Gets target languages for translation from employees' preferred languages.
    /// Returns unique non-English languages from all employees in the tenant.
    /// </summary>
    private async Task<List<string>> GetTargetLanguagesFromEmployeesAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Query all non-null/empty PreferredLanguage values for this tenant
            // NOTE: We use IgnoreQueryFilters() because this method runs in a background job context
            // where ICurrentUserService.TenantId may not be set correctly for the global tenant filter.
            // Since we explicitly filter by tenantId and IsDeleted, this is safe.
            var employeeLanguages = await _coreDbContext.Employees
                .IgnoreQueryFilters()
                .Where(e => e.TenantId == tenantId
                    && !e.IsDeleted
                    && !string.IsNullOrEmpty(e.PreferredLanguage))
                .Select(e => e.PreferredLanguage)
                .Distinct()
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "[Auto-Transcription] Raw language query returned {Count} results for tenant {TenantId}: [{Languages}]",
                employeeLanguages.Count, tenantId, string.Join(", ", employeeLanguages));

            // Filter out null/whitespace values that might have slipped through
            var validLanguages = employeeLanguages
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            _logger.LogInformation(
                "[Auto-Transcription] After whitespace filter: {Count} valid languages: [{Languages}]",
                validLanguages.Count, string.Join(", ", validLanguages));

            if (validLanguages.Count == 0)
            {
                _logger.LogInformation(
                    "[Auto-Transcription] No employee language preferences found for tenant {TenantId}. Using English only.",
                    tenantId);
                return new List<string> { "English" };
            }

            // Normalize languages - convert codes to names where applicable
            var normalizedLanguages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var lang in validLanguages)
            {
                // If it's a language code (2-3 chars), convert to name
                if (lang!.Length <= 3)
                {
                    var name = await _languageCodeService.GetLanguageNameAsync(lang);
                    normalizedLanguages.Add(name);
                    _logger.LogDebug(
                        "[Auto-Transcription] Converted language code '{Code}' to name '{Name}'",
                        lang, name);
                }
                else
                {
                    normalizedLanguages.Add(lang);
                }
            }

            // Always include English
            normalizedLanguages.Add("English");

            // Identify non-English languages for translation
            var nonEnglishLanguages = normalizedLanguages
                .Where(l => !l.Equals("English", StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.LogInformation(
                "[Auto-Transcription] Found {Total} target languages ({NonEnglishCount} non-English) from employees for tenant {TenantId}: [{Languages}]",
                normalizedLanguages.Count, nonEnglishLanguages.Count, tenantId, string.Join(", ", normalizedLanguages));

            if (nonEnglishLanguages.Count == 0)
            {
                _logger.LogInformation(
                    "[Auto-Transcription] All employee languages resolved to English. Only English subtitles will be generated.");
            }

            return normalizedLanguages.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Auto-Transcription] Failed to get employee languages for tenant {TenantId}. Using English only.",
                tenantId);
            return new List<string> { "English" };
        }
    }

    /// <summary>
    /// Result of an auto-transcription attempt.
    /// </summary>
    private class AutoTranscribeResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public VideoContentInfo? VideoContent { get; init; }

        public static AutoTranscribeResult SuccessResult(VideoContentInfo videoContent) =>
            new()
            {
                Success = true,
                VideoContent = videoContent
            };

        public static AutoTranscribeResult Failure(string errorMessage) =>
            new()
            {
                Success = false,
                ErrorMessage = errorMessage
            };
    }

    /// <summary>
    /// Converts technical video extraction errors to user-friendly messages.
    /// </summary>
    private static string GetUserFriendlyVideoError(string? technicalError)
    {
        if (string.IsNullOrEmpty(technicalError))
            return "Video transcription failed. Please try again or use a different video.";

        var lowerError = technicalError.ToLowerInvariant();

        if (lowerError.Contains("copying content to a stream") || lowerError.Contains("stream"))
            return "The video file could not be processed. This may be a temporary issue - please try again later.";

        if (lowerError.Contains("timeout") || lowerError.Contains("timed out"))
            return "Video processing timed out. The video may be too long - try a shorter video or check your connection.";

        if (lowerError.Contains("http request failed") || lowerError.Contains("network"))
            return "Video could not be downloaded. Please check that the video URL is accessible.";

        if (lowerError.Contains("api key") || lowerError.Contains("not configured"))
            return "Video transcription service is not configured. Please contact support.";

        if (lowerError.Contains("transcription failed"))
            return "Video transcription service is temporarily unavailable. Please try again later.";

        if (lowerError.Contains("format") || lowerError.Contains("codec") || lowerError.Contains("unsupported"))
            return "The video format is not supported. Please use MP4, MOV, AVI, or WebM format.";

        if (lowerError.Contains("not found") || lowerError.Contains("404"))
            return "The video file could not be found. Please check the video URL is correct.";

        if (lowerError.Contains("access denied") || lowerError.Contains("403") || lowerError.Contains("unauthorized"))
            return "Cannot access the video file. Please check the video permissions or try uploading directly.";

        // Default message
        return "Video transcription unavailable";
    }

    /// <summary>
    /// Converts technical PDF extraction errors to user-friendly messages.
    /// </summary>
    private static string GetUserFriendlyPdfError(string? technicalError)
    {
        if (string.IsNullOrEmpty(technicalError))
            return "PDF extraction failed. Please try again or use a different PDF.";

        var lowerError = technicalError.ToLowerInvariant();

        if (lowerError.Contains("password") || lowerError.Contains("encrypted"))
            return "The PDF is password-protected. Please use an unprotected PDF.";

        if (lowerError.Contains("corrupt") || lowerError.Contains("invalid"))
            return "The PDF appears to be corrupted. Please try uploading a different file.";

        if (lowerError.Contains("scanned") || lowerError.Contains("ocr") || lowerError.Contains("image"))
            return "The PDF appears to be a scanned document. Text-based PDFs work better.";

        if (lowerError.Contains("timeout") || lowerError.Contains("timed out"))
            return "PDF processing timed out. The document may be too large.";

        if (lowerError.Contains("not found") || lowerError.Contains("404"))
            return "The PDF file could not be found. Please try uploading it again.";

        if (lowerError.Contains("access denied") || lowerError.Contains("403"))
            return "Cannot access the PDF file. Please try uploading it again.";

        // Default message
        return "PDF extraction unavailable";
    }
}
