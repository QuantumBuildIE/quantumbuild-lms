using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Transcribes the video attached to a new-wizard ToolboxTalk via ElevenLabs.
/// On success, chains ContentCreationParseJobForTalk to materialise sections.
/// Analogous to VideoTranscriptionJob (session-based) but targets the talk row directly.
/// </summary>
[AutomaticRetry(Attempts = 2)]
[Queue("content-generation")]
public class VideoTranscriptionJobForTalk(
    IToolboxTalksDbContext dbContext,
    ITranscriptionService transcriptionService,
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

            var transcriptText = string.Join(" ", result.Words
                .Where(w => w.Type == "word")
                .Select(w => w.Text));

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
}
