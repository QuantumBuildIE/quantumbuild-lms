using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Transcribes the video attached to a new-wizard ToolboxTalk via ElevenLabs.
/// On success, chains ContentCreationParseJobForTalk to materialise sections and kicks off
/// subtitle generation (reusing the cached transcript words, no second ElevenLabs call).
/// Analogous to VideoTranscriptionJob (session-based) but targets the talk row directly.
/// </summary>
[AutomaticRetry(Attempts = 2)]
[Queue("content-generation")]
public class VideoTranscriptionJobForTalk(
    IToolboxTalksDbContext dbContext,
    ITranscriptionService transcriptionService,
    ISrtGeneratorService srtGeneratorService,
    ITranscriptService transcriptService,
    ISubtitleProcessingOrchestrator subtitleOrchestrator,
    IContentExtractionService contentExtractionService,
    IOptions<SubtitleProcessingSettings> subtitleSettings,
    ILogger<VideoTranscriptionJobForTalk> logger)
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task ExecuteAsync(Guid talkId, Guid tenantId, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[VideoTranscriptionForTalk] Starting for talk {TalkId}, tenant {TenantId}",
            talkId, tenantId);

        var talk = await dbContext.ToolboxTalks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == talkId && t.TenantId == tenantId, cancellationToken);

        if (talk is null)
        {
            logger.LogWarning(
                "[VideoTranscriptionForTalk] Talk {TalkId} not found for tenant {TenantId}",
                talkId, tenantId);
            return;
        }

        if (talk.IsDeleted)
        {
            logger.LogInformation(
                "[VideoTranscriptionForTalk] Talk {TalkId} has been deleted — skipping",
                talkId);
            return;
        }

        if (talk.Status != ToolboxTalkStatus.Processing)
        {
            logger.LogWarning(
                "[VideoTranscriptionForTalk] Talk {TalkId} status is {Status}, expected Processing — skipping",
                talkId, talk.Status);
            return;
        }

        // Uploaded video file takes precedence over a manually entered URL
        var videoUrl = talk.SourceFileType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true
            ? talk.SourceFileUrl
            : talk.VideoUrl;

        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            logger.LogError(
                "[VideoTranscriptionForTalk] Talk {TalkId} has no video URL — reverting to Draft",
                talkId);
            talk.Status = ToolboxTalkStatus.Draft;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var result = await transcriptionService.TranscribeAsync(videoUrl, cancellationToken);

            if (!result.Success)
            {
                logger.LogError(
                    "[VideoTranscriptionForTalk] Transcription failed for talk {TalkId}: {Error}",
                    talkId, result.ErrorMessage);
                talk.Status = ToolboxTalkStatus.Draft;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var srt = srtGeneratorService.GenerateSrt(result.Words, subtitleSettings.Value.WordsPerSubtitle);
            var parsed = transcriptService.ParseSrtContent(srt, null);
            var transcriptText = transcriptService.GetCleanFullText(parsed);

            if (string.IsNullOrWhiteSpace(transcriptText))
            {
                logger.LogError(
                    "[VideoTranscriptionForTalk] Transcription returned no word content for talk {TalkId}",
                    talkId);
                talk.Status = ToolboxTalkStatus.Draft;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            talk.ExtractedVideoTranscript = transcriptText;
            talk.TranscriptWordsJson = JsonSerializer.Serialize(result.Words, CamelCaseOptions);
            talk.VideoTranscriptExtractedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "[VideoTranscriptionForTalk] Transcription complete for talk {TalkId}: {WordCount} words",
                talkId, result.Words.Count(w => w.Type == "word"));

            await StartSubtitleProcessingAsync(talkId, tenantId, videoUrl, talk.TranscriptWordsJson, cancellationToken);

            BackgroundJob.Enqueue<ContentCreationParseJobForTalk>(
                job => job.ExecuteAsync(talkId, tenantId, CancellationToken.None));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[VideoTranscriptionForTalk] Unhandled error transcribing talk {TalkId}", talkId);
            try
            {
                talk.Status = ToolboxTalkStatus.Draft;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx,
                    "[VideoTranscriptionForTalk] Failed to revert status for talk {TalkId}", talkId);
            }
        }
    }

    /// <summary>
    /// Kicks off subtitle generation for the talk's video, reusing the transcript words already
    /// fetched from ElevenLabs above (via cachedTranscriptWordsJson) so no redundant
    /// transcription call/cost is incurred. Mirrors the pattern the session-based wizard uses at
    /// ContentCreationSessionService.cs:733-739. Target languages are resolved the same way
    /// legacy's auto-transcription path does — English plus each distinct language spoken by an
    /// active tenant employee (IContentExtractionService.GetTargetLanguagesFromEmployeesAsync) —
    /// since this runs immediately after transcription, before any wizard translate step exists
    /// to consult. Failures here are logged and swallowed: missing/late subtitles should never
    /// revert the talk to Draft or block section-parsing.
    /// </summary>
    private async Task StartSubtitleProcessingAsync(
        Guid talkId,
        Guid tenantId,
        string videoUrl,
        string transcriptWordsJson,
        CancellationToken cancellationToken)
    {
        try
        {
            var targetLanguages = await contentExtractionService.GetTargetLanguagesFromEmployeesAsync(
                tenantId, cancellationToken);

            var subtitleJobId = await subtitleOrchestrator.StartProcessingAsync(
                talkId,
                videoUrl,
                SubtitleVideoSourceType.DirectUrl,
                targetLanguages,
                cachedTranscriptWordsJson: transcriptWordsJson,
                cancellationToken: cancellationToken);

            logger.LogInformation(
                "[VideoTranscriptionForTalk] Started subtitle processing for talk {TalkId}, job {SubtitleJobId}, languages: {Languages}",
                talkId, subtitleJobId, string.Join(", ", targetLanguages));
        }
        catch (Exception ex)
        {
            // Do not let a subtitle-processing failure (e.g. a job already active for this talk)
            // revert the talk's status or block section-parsing — subtitles are best-effort here.
            logger.LogError(ex,
                "[VideoTranscriptionForTalk] Failed to start subtitle processing for talk {TalkId}",
                talkId);
        }
    }
}
