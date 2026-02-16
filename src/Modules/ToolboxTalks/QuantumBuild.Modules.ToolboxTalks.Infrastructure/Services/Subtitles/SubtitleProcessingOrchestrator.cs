using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;

/// <summary>
/// Orchestrates the subtitle processing workflow:
/// 1. Get video URL from source provider
/// 2. Transcribe audio via ElevenLabs
/// 3. Generate SRT from transcription
/// 4. Upload English SRT
/// 5. Translate to each target language via Claude
/// 6. Upload translated SRTs
/// 7. Report progress via SignalR throughout
/// </summary>
public class SubtitleProcessingOrchestrator : ISubtitleProcessingOrchestrator
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly IVideoSourceProvider _videoSourceProvider;
    private readonly ITranscriptionService _transcriptionService;
    private readonly ITranslationService _translationService;
    private readonly ISrtStorageProvider _srtStorageProvider;
    private readonly ISrtGeneratorService _srtGeneratorService;
    private readonly ILanguageCodeService _languageCodeService;
    private readonly ISubtitleProgressReporter _progressReporter;
    private readonly SubtitleProcessingSettings _settings;
    private readonly ILogger<SubtitleProcessingOrchestrator> _logger;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public SubtitleProcessingOrchestrator(
        IToolboxTalksDbContext dbContext,
        IVideoSourceProvider videoSourceProvider,
        ITranscriptionService transcriptionService,
        ITranslationService translationService,
        ISrtStorageProvider srtStorageProvider,
        ISrtGeneratorService srtGeneratorService,
        ILanguageCodeService languageCodeService,
        ISubtitleProgressReporter progressReporter,
        IOptions<SubtitleProcessingSettings> settings,
        ILogger<SubtitleProcessingOrchestrator> logger,
        IBackgroundJobClient backgroundJobClient)
    {
        _dbContext = dbContext;
        _videoSourceProvider = videoSourceProvider;
        _transcriptionService = transcriptionService;
        _translationService = translationService;
        _srtStorageProvider = srtStorageProvider;
        _srtGeneratorService = srtGeneratorService;
        _languageCodeService = languageCodeService;
        _progressReporter = progressReporter;
        _settings = settings.Value;
        _logger = logger;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <inheritdoc />
    public async Task<Guid> StartProcessingAsync(
        Guid toolboxTalkId,
        string videoUrl,
        SubtitleVideoSourceType sourceType,
        List<string> targetLanguages,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting subtitle processing for ToolboxTalk {TalkId}", toolboxTalkId);

        // Verify the toolbox talk exists
        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId && !t.IsDeleted, cancellationToken);

        if (talk == null)
            throw new InvalidOperationException($"ToolboxTalk {toolboxTalkId} not found");

        // Check for existing active job
        var existingJob = await _dbContext.SubtitleProcessingJobs
            .FirstOrDefaultAsync(j => j.ToolboxTalkId == toolboxTalkId
                && !j.IsDeleted
                && j.Status != SubtitleProcessingStatus.Completed
                && j.Status != SubtitleProcessingStatus.Failed,
                cancellationToken);

        if (existingJob != null)
            throw new InvalidOperationException($"A processing job is already active for this talk. Job ID: {existingJob.Id}");

        // Create the job record
        var job = new SubtitleProcessingJob
        {
            ToolboxTalkId = toolboxTalkId,
            SourceVideoUrl = videoUrl,
            VideoSourceType = sourceType,
            Status = SubtitleProcessingStatus.Pending,
            StartedAt = DateTime.UtcNow
        };

        // Add English as the first translation (source)
        job.Translations.Add(new SubtitleTranslation
        {
            Language = "English",
            LanguageCode = "en",
            Status = SubtitleTranslationStatus.Pending,
            TotalSubtitles = 0,
            SubtitlesProcessed = 0
        });

        // Add target languages (excluding English if specified)
        foreach (var language in targetLanguages.Where(l => !l.Equals("English", StringComparison.OrdinalIgnoreCase)))
        {
            job.Translations.Add(new SubtitleTranslation
            {
                Language = language,
                LanguageCode = _languageCodeService.GetLanguageCode(language),
                Status = SubtitleTranslationStatus.Pending,
                TotalSubtitles = 0,
                SubtitlesProcessed = 0
            });
        }

        _dbContext.SubtitleProcessingJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Queue the background job
        _backgroundJobClient.Enqueue<ISubtitleProcessingOrchestrator>(
            orchestrator => orchestrator.ProcessAsync(job.Id, CancellationToken.None));

        _logger.LogInformation("Subtitle processing job {JobId} created and queued for ToolboxTalk {TalkId}",
            job.Id, toolboxTalkId);

        return job.Id;
    }

    /// <inheritdoc />
    public async Task ProcessAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.SubtitleProcessingJobs
            .Include(j => j.Translations)
            .Include(j => j.ToolboxTalk)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogError("Job {JobId} not found", jobId);
            return;
        }

        try
        {
            _logger.LogInformation("Processing job {JobId} for ToolboxTalk {TalkId}", jobId, job.ToolboxTalkId);

            // Step 1: Get direct video URL
            await UpdateStatusAsync(job, SubtitleProcessingStatus.Transcribing, "Getting video URL...", 2, cancellationToken);

            var videoResult = await _videoSourceProvider.GetDirectUrlAsync(
                job.SourceVideoUrl, job.VideoSourceType, cancellationToken);

            if (!videoResult.Success)
            {
                await FailJobAsync(job, $"Failed to get video URL: {videoResult.ErrorMessage}", cancellationToken);
                return;
            }

            // Step 2: Transcribe video
            await UpdateStatusAsync(job, SubtitleProcessingStatus.Transcribing, "Transcribing audio...", 5, cancellationToken);

            var transcriptionResult = await _transcriptionService.TranscribeAsync(
                videoResult.DirectUrl!, cancellationToken);

            if (!transcriptionResult.Success)
            {
                await FailJobAsync(job, $"Transcription failed: {transcriptionResult.ErrorMessage}", cancellationToken);
                return;
            }

            // Step 3: Generate English SRT
            await UpdateStatusAsync(job, SubtitleProcessingStatus.Transcribing, "Generating subtitles...", 10, cancellationToken);

            var englishSrt = _srtGeneratorService.GenerateSrt(
                transcriptionResult.Words, _settings.WordsPerSubtitle);

            job.EnglishSrtContent = englishSrt;
            job.TotalSubtitles = _srtGeneratorService.CountSubtitleBlocks(englishSrt);

            // Update all translations with total count
            foreach (var translation in job.Translations)
            {
                translation.TotalSubtitles = job.TotalSubtitles;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Generated {SubtitleCount} subtitle blocks for job {JobId}",
                job.TotalSubtitles, jobId);

            // Step 4: Upload English SRT
            await UpdateStatusAsync(job, SubtitleProcessingStatus.Uploading, "Uploading English subtitles...", 15, cancellationToken);

            var videoSlug = GenerateVideoSlug(job.ToolboxTalk.Title);
            var englishFileName = $"{videoSlug}_en.srt";

            var englishUpload = await _srtStorageProvider.UploadSrtAsync(
                englishSrt, englishFileName, job.TenantId, cancellationToken);

            var englishTranslation = job.Translations.First(t => t.Language == "English");
            englishTranslation.SrtContent = englishSrt;
            englishTranslation.SrtUrl = englishUpload.Success ? englishUpload.Url : null;
            englishTranslation.Status = englishUpload.Success
                ? SubtitleTranslationStatus.Completed
                : SubtitleTranslationStatus.Failed;
            englishTranslation.SubtitlesProcessed = job.TotalSubtitles;

            if (!englishUpload.Success)
            {
                englishTranslation.ErrorMessage = englishUpload.ErrorMessage;
                _logger.LogWarning("Failed to upload English SRT: {Error}", englishUpload.ErrorMessage);
            }

            job.EnglishSrtUrl = englishUpload.Url;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Step 5: Translate to each target language
            var targetLanguages = job.Translations
                .Where(t => t.Language != "English")
                .ToList();

            if (targetLanguages.Count > 0)
            {
                job.Status = SubtitleProcessingStatus.Translating;
                await _dbContext.SaveChangesAsync(cancellationToken);

                var progressPerLanguage = 80 / targetLanguages.Count; // 80% of progress for translations (15-95)
                var currentProgress = 15;

                foreach (var translation in targetLanguages)
                {
                    await TranslateLanguageAsync(
                        job, translation, englishSrt, videoSlug,
                        currentProgress, progressPerLanguage, cancellationToken);

                    currentProgress += progressPerLanguage;
                }
            }

            // Step 6: Complete
            job.Status = SubtitleProcessingStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await UpdateStatusAsync(job, SubtitleProcessingStatus.Completed, "Processing complete!", 100, cancellationToken);

            _logger.LogInformation("Job {JobId} completed successfully", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed with exception", jobId);
            await FailJobAsync(job, $"Processing failed: {ex.Message}", cancellationToken);
        }
    }

    private async Task TranslateLanguageAsync(
        SubtitleProcessingJob job,
        SubtitleTranslation translation,
        string englishSrt,
        string videoSlug,
        int baseProgress,
        int progressWeight,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Translating to {Language} for job {JobId}", translation.Language, job.Id);

        translation.Status = SubtitleTranslationStatus.InProgress;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var blocks = _srtGeneratorService.SplitSrtIntoBlocks(englishSrt);
            var translatedSrt = new StringBuilder();
            var batchSize = _settings.BatchSize;

            for (int i = 0; i < blocks.Count; i += batchSize)
            {
                var batchBlocks = blocks.Skip(i).Take(batchSize).ToList();
                var batchContent = string.Join("\n\n", batchBlocks);

                var translateResult = await _translationService.TranslateSrtBatchAsync(
                    batchContent, translation.Language, cancellationToken);

                if (translateResult.Success)
                {
                    translatedSrt.Append(translateResult.TranslatedContent);
                    if (i + batchSize < blocks.Count)
                        translatedSrt.Append("\n\n");
                }
                else
                {
                    // Use original on failure
                    translatedSrt.Append(batchContent);
                    if (i + batchSize < blocks.Count)
                        translatedSrt.Append("\n\n");

                    _logger.LogWarning("Batch translation failed for {Language}, using original. Error: {Error}",
                        translation.Language, translateResult.ErrorMessage);
                }

                // Update progress
                translation.SubtitlesProcessed = Math.Min(i + batchSize, blocks.Count);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var langProgress = blocks.Count > 0
                    ? (translation.SubtitlesProcessed * 100) / blocks.Count
                    : 0;
                var overallProgress = baseProgress + (langProgress * progressWeight / 100);

                await UpdateStatusAsync(job, SubtitleProcessingStatus.Translating,
                    $"Translating {translation.Language}... ({translation.SubtitlesProcessed}/{blocks.Count})",
                    overallProgress, cancellationToken);
            }

            // Upload translated SRT
            var fileName = $"{videoSlug}_{translation.LanguageCode}.srt";
            var uploadResult = await _srtStorageProvider.UploadSrtAsync(
                translatedSrt.ToString(), fileName, job.TenantId, cancellationToken);

            translation.SrtContent = translatedSrt.ToString();
            translation.SrtUrl = uploadResult.Success ? uploadResult.Url : null;
            translation.Status = SubtitleTranslationStatus.Completed;

            if (!uploadResult.Success)
            {
                translation.ErrorMessage = $"Upload failed: {uploadResult.ErrorMessage}";
                _logger.LogWarning("Failed to upload {Language} SRT: {Error}",
                    translation.Language, uploadResult.ErrorMessage);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Completed translation to {Language} for job {JobId}",
                translation.Language, job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation to {Language} failed for job {JobId}",
                translation.Language, job.Id);

            translation.Status = SubtitleTranslationStatus.Failed;
            translation.ErrorMessage = ex.Message;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<SubtitleProcessingStatusDto?> GetStatusAsync(
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.SubtitleProcessingJobs
            .Include(j => j.Translations)
            .Where(j => j.ToolboxTalkId == toolboxTalkId && !j.IsDeleted)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job == null)
            return null;

        return MapToStatusDto(job);
    }

    /// <inheritdoc />
    public async Task<bool> CancelProcessingAsync(
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.SubtitleProcessingJobs
            .Include(j => j.Translations)
            .Where(j => j.ToolboxTalkId == toolboxTalkId && !j.IsDeleted)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job == null)
            return false;

        // Check if job is already completed or failed
        if (job.Status == SubtitleProcessingStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed job");
        if (job.Status == SubtitleProcessingStatus.Failed)
            throw new InvalidOperationException("Cannot cancel a failed job");
        if (job.Status == SubtitleProcessingStatus.Cancelled)
            throw new InvalidOperationException("Job is already cancelled");

        _logger.LogInformation("Cancelling subtitle processing job {JobId} for ToolboxTalk {TalkId}",
            job.Id, toolboxTalkId);

        job.Status = SubtitleProcessingStatus.Cancelled;
        job.ErrorMessage = "Processing was cancelled by user";
        job.CompletedAt = DateTime.UtcNow;

        // Mark any in-progress translations as failed
        foreach (var translation in job.Translations.Where(t => t.Status == SubtitleTranslationStatus.InProgress))
        {
            translation.Status = SubtitleTranslationStatus.Failed;
            translation.ErrorMessage = "Cancelled by user";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _progressReporter.ReportProgressAsync(job.Id, new SubtitleProgressUpdate
        {
            OverallStatus = SubtitleProcessingStatus.Cancelled,
            OverallPercentage = 0,
            CurrentStep = "Cancelled",
            ErrorMessage = "Processing was cancelled by user"
        }, cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<Guid?> RetryFailedTranslationsAsync(
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.SubtitleProcessingJobs
            .Include(j => j.Translations)
            .Include(j => j.ToolboxTalk)
            .Where(j => j.ToolboxTalkId == toolboxTalkId && !j.IsDeleted)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job == null)
            throw new InvalidOperationException("No processing job found for this toolbox talk");

        var failedTranslations = job.Translations
            .Where(t => t.Status == SubtitleTranslationStatus.Failed)
            .ToList();

        if (failedTranslations.Count == 0)
            throw new InvalidOperationException("No failed translations to retry");

        // Check that we have English SRT content to translate from
        if (string.IsNullOrEmpty(job.EnglishSrtContent))
            throw new InvalidOperationException("English subtitles not available. Please start a new processing job.");

        _logger.LogInformation("Retrying {Count} failed translations for job {JobId}",
            failedTranslations.Count, job.Id);

        // Reset failed translations to pending
        foreach (var translation in failedTranslations)
        {
            translation.Status = SubtitleTranslationStatus.Pending;
            translation.ErrorMessage = null;
            translation.SubtitlesProcessed = 0;
        }

        // Update job status back to translating
        job.Status = SubtitleProcessingStatus.Translating;
        job.ErrorMessage = null;
        job.CompletedAt = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Queue retry background job
        _backgroundJobClient.Enqueue<ISubtitleProcessingOrchestrator>(
            orchestrator => orchestrator.ProcessRetryAsync(job.Id, CancellationToken.None));

        return job.Id;
    }

    /// <summary>
    /// Processes retried translations. Called by Hangfire background job.
    /// </summary>
    public async Task ProcessRetryAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.SubtitleProcessingJobs
            .Include(j => j.Translations)
            .Include(j => j.ToolboxTalk)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            _logger.LogError("Job {JobId} not found for retry", jobId);
            return;
        }

        try
        {
            var pendingTranslations = job.Translations
                .Where(t => t.Status == SubtitleTranslationStatus.Pending && t.Language != "English")
                .ToList();

            if (pendingTranslations.Count == 0)
            {
                _logger.LogWarning("No pending translations found for retry in job {JobId}", jobId);
                job.Status = SubtitleProcessingStatus.Completed;
                job.CompletedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var videoSlug = GenerateVideoSlug(job.ToolboxTalk.Title);
            var progressPerLanguage = 80 / pendingTranslations.Count;
            var currentProgress = 15;

            foreach (var translation in pendingTranslations)
            {
                await TranslateLanguageAsync(
                    job, translation, job.EnglishSrtContent!, videoSlug,
                    currentProgress, progressPerLanguage, cancellationToken);

                currentProgress += progressPerLanguage;
            }

            // Complete
            job.Status = SubtitleProcessingStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await UpdateStatusAsync(job, SubtitleProcessingStatus.Completed, "Processing complete!", 100, cancellationToken);

            _logger.LogInformation("Job {JobId} retry completed successfully", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} retry failed with exception", jobId);
            await FailJobAsync(job, $"Retry failed: {ex.Message}", cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<int> TranslateMissingLanguagesAsync(
        Guid toolboxTalkId,
        Guid tenantId,
        List<string> missingLanguageCodes,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "TranslateMissingLanguagesAsync started for ToolboxTalk {ToolboxTalkId}. " +
            "Missing languages: {Languages}",
            toolboxTalkId, string.Join(", ", missingLanguageCodes));

        // Find the latest completed subtitle processing job for this talk
        var job = await _dbContext.SubtitleProcessingJobs
            .IgnoreQueryFilters()
            .Include(j => j.Translations)
            .Include(j => j.ToolboxTalk)
            .Where(j => j.ToolboxTalkId == toolboxTalkId
                && j.TenantId == tenantId
                && !j.IsDeleted
                && j.Status == SubtitleProcessingStatus.Completed)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job == null)
        {
            _logger.LogInformation(
                "No completed subtitle job found for ToolboxTalk {ToolboxTalkId}. " +
                "Skipping subtitle translation for missing languages.",
                toolboxTalkId);
            return 0;
        }

        if (string.IsNullOrEmpty(job.EnglishSrtContent))
        {
            _logger.LogWarning(
                "Subtitle job {JobId} has no English SRT content. " +
                "Cannot translate to missing languages.",
                job.Id);
            return 0;
        }

        // Filter to only truly missing languages (not already translated in this job)
        var existingLanguageCodes = job.Translations
            .Select(t => t.LanguageCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var actuallyMissing = missingLanguageCodes
            .Where(code => !existingLanguageCodes.Contains(code))
            .ToList();

        if (actuallyMissing.Count == 0)
        {
            _logger.LogInformation(
                "All requested subtitle languages already exist for job {JobId}. Nothing to translate.",
                job.Id);
            return 0;
        }

        _logger.LogInformation(
            "Translating subtitles for {Count} missing language(s) on job {JobId}: {Languages}",
            actuallyMissing.Count, job.Id, string.Join(", ", actuallyMissing));

        var videoSlug = GenerateVideoSlug(job.ToolboxTalk.Title);
        var translated = 0;

        foreach (var languageCode in actuallyMissing)
        {
            try
            {
                var languageName = _languageCodeService.GetLanguageName(languageCode);

                var translation = new SubtitleTranslation
                {
                    SubtitleProcessingJobId = job.Id,
                    Language = languageName,
                    LanguageCode = languageCode,
                    Status = SubtitleTranslationStatus.Pending,
                    TotalSubtitles = job.TotalSubtitles,
                    SubtitlesProcessed = 0
                };

                _dbContext.SubtitleTranslations.Add(translation);
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Reuse the existing TranslateLanguageAsync method
                await TranslateLanguageAsync(
                    job, translation, job.EnglishSrtContent, videoSlug,
                    0, 100, cancellationToken);

                if (translation.Status == SubtitleTranslationStatus.Completed)
                {
                    translated++;
                    _logger.LogInformation(
                        "Subtitle translation to {Language} ({Code}) completed for job {JobId}",
                        languageName, languageCode, job.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "Subtitle translation to {Language} ({Code}) failed for job {JobId}: {Error}",
                        languageName, languageCode, job.Id, translation.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error translating subtitles to {LanguageCode} for job {JobId}",
                    languageCode, job.Id);
            }
        }

        // Restore the job status to Completed since TranslateLanguageAsync
        // changes it to Translating during processing
        if (translated > 0)
        {
            job.Status = SubtitleProcessingStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "TranslateMissingLanguagesAsync completed for ToolboxTalk {ToolboxTalkId}. " +
            "Translated: {Translated}/{Total}",
            toolboxTalkId, translated, actuallyMissing.Count);

        return translated;
    }

    /// <inheritdoc />
    public async Task<string?> GetSrtContentAsync(
        Guid toolboxTalkId,
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.SubtitleProcessingJobs
            .Include(j => j.Translations)
            .Where(j => j.ToolboxTalkId == toolboxTalkId && !j.IsDeleted)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job == null)
            return null;

        var translation = job.Translations
            .FirstOrDefault(t => t.LanguageCode.Equals(languageCode, StringComparison.OrdinalIgnoreCase));

        if (translation == null || translation.Status != SubtitleTranslationStatus.Completed)
            return null;

        return translation.SrtContent;
    }

    private async Task UpdateStatusAsync(
        SubtitleProcessingJob job,
        SubtitleProcessingStatus status,
        string currentStep,
        int percentage,
        CancellationToken cancellationToken)
    {
        job.Status = status;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var update = new SubtitleProgressUpdate
        {
            OverallStatus = status,
            OverallPercentage = percentage,
            CurrentStep = currentStep,
            Languages = job.Translations.Select(t => new LanguageProgressInfo
            {
                Language = t.Language,
                LanguageCode = t.LanguageCode,
                Status = t.Status,
                Percentage = t.TotalSubtitles > 0
                    ? (t.SubtitlesProcessed * 100) / t.TotalSubtitles
                    : 0,
                SrtUrl = t.SrtUrl
            }).ToList()
        };

        await _progressReporter.ReportProgressAsync(job.Id, update, cancellationToken);
    }

    private async Task FailJobAsync(
        SubtitleProcessingJob job,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        job.Status = SubtitleProcessingStatus.Failed;
        job.ErrorMessage = errorMessage;
        job.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _progressReporter.ReportProgressAsync(job.Id, new SubtitleProgressUpdate
        {
            OverallStatus = SubtitleProcessingStatus.Failed,
            OverallPercentage = 0,
            CurrentStep = "Failed",
            ErrorMessage = errorMessage
        }, cancellationToken);

        _logger.LogError("Job {JobId} failed: {Error}", job.Id, errorMessage);
    }

    private static string GenerateVideoSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\s]", "");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"\s+", "_");
        return slug.Trim('_');
    }

    private static SubtitleProcessingStatusDto MapToStatusDto(SubtitleProcessingJob job)
    {
        var completedLanguages = job.Translations.Count(t => t.Status == SubtitleTranslationStatus.Completed);
        var totalLanguages = job.Translations.Count;

        var overallPercentage = job.Status switch
        {
            SubtitleProcessingStatus.Pending => 0,
            SubtitleProcessingStatus.Transcribing => 10,
            SubtitleProcessingStatus.Translating => totalLanguages > 0
                ? 15 + (completedLanguages * 80 / totalLanguages)
                : 15,
            SubtitleProcessingStatus.Uploading => 95,
            SubtitleProcessingStatus.Completed => 100,
            SubtitleProcessingStatus.Failed => 0,
            SubtitleProcessingStatus.Cancelled => 0,
            _ => 0
        };

        return new SubtitleProcessingStatusDto
        {
            JobId = job.Id,
            ToolboxTalkId = job.ToolboxTalkId,
            Status = job.Status,
            OverallPercentage = overallPercentage,
            CurrentStep = GetCurrentStep(job),
            ErrorMessage = job.ErrorMessage,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            TotalSubtitles = job.TotalSubtitles,
            Languages = job.Translations.Select(t => new LanguageStatusDto
            {
                Language = t.Language,
                LanguageCode = t.LanguageCode,
                Status = t.Status,
                Percentage = t.TotalSubtitles > 0
                    ? (t.SubtitlesProcessed * 100) / t.TotalSubtitles
                    : 0,
                SrtUrl = t.SrtUrl,
                ErrorMessage = t.ErrorMessage
            }).ToList()
        };
    }

    private static string GetCurrentStep(SubtitleProcessingJob job)
    {
        return job.Status switch
        {
            SubtitleProcessingStatus.Pending => "Waiting to start...",
            SubtitleProcessingStatus.Transcribing => "Transcribing audio...",
            SubtitleProcessingStatus.Translating => GetTranslatingStep(job),
            SubtitleProcessingStatus.Uploading => "Uploading files...",
            SubtitleProcessingStatus.Completed => "Complete!",
            SubtitleProcessingStatus.Failed => "Failed",
            SubtitleProcessingStatus.Cancelled => "Cancelled",
            _ => "Unknown"
        };
    }

    private static string GetTranslatingStep(SubtitleProcessingJob job)
    {
        var inProgress = job.Translations.FirstOrDefault(t => t.Status == SubtitleTranslationStatus.InProgress);
        if (inProgress != null)
        {
            return $"Translating {inProgress.Language}... ({inProgress.SubtitlesProcessed}/{inProgress.TotalSubtitles})";
        }
        return "Translating...";
    }
}
