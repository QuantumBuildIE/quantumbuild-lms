using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Background job that transcribes a video file via ElevenLabs and stores
/// the transcript on the ContentCreationSession. On success, enqueues
/// ContentCreationParseJob for the next pipeline step.
/// </summary>
[AutomaticRetry(Attempts = 2)]
[Queue("content-generation")]
public class VideoTranscriptionJob(
    IToolboxTalksDbContext dbContext,
    ITranscriptionService transcriptionService,
    ILogger<VideoTranscriptionJob> logger)
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task ExecuteAsync(Guid sessionId, Guid tenantId, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[VideoTranscription] Starting transcription for session {SessionId}, tenant {TenantId}",
            sessionId, tenantId);

        var session = await dbContext.ContentCreationSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.TenantId == tenantId, cancellationToken);

        if (session is null)
        {
            logger.LogWarning(
                "[VideoTranscription] Session {SessionId} not found for tenant {TenantId}",
                sessionId, tenantId);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.SourceFileUrl))
        {
            logger.LogWarning(
                "[VideoTranscription] Session {SessionId} has no SourceFileUrl — marking as Failed",
                sessionId);
            session.Status = ContentCreationSessionStatus.Failed;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            // Mark as transcribing
            session.Status = ContentCreationSessionStatus.Transcribing;
            await dbContext.SaveChangesAsync(cancellationToken);

            var result = await transcriptionService.TranscribeAsync(session.SourceFileUrl, cancellationToken);

            if (!result.Success)
            {
                logger.LogError(
                    "[VideoTranscription] Transcription failed for session {SessionId}: {Error}",
                    sessionId, result.ErrorMessage);
                session.Status = ContentCreationSessionStatus.Failed;
                session.ErrorMessage = result.ErrorMessage;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Join words into plain text — same logic as the synchronous path
            var transcriptText = string.Join(" ", result.Words
                .Where(w => w.Type == "word")
                .Select(w => w.Text));

            if (string.IsNullOrWhiteSpace(transcriptText))
            {
                logger.LogError(
                    "[VideoTranscription] Transcription returned no word content for session {SessionId}",
                    sessionId);
                session.Status = ContentCreationSessionStatus.Failed;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            session.TranscriptText = transcriptText;
            session.TranscriptWordsJson = JsonSerializer.Serialize(result.Words, CamelCaseOptions);
            session.Status = ContentCreationSessionStatus.Parsing;
            await dbContext.SaveChangesAsync(cancellationToken);

            var wordCount = result.Words.Count(w => w.Type == "word");
            logger.LogInformation(
                "[VideoTranscription] Transcription complete for session {SessionId}: {WordCount} words, {Length} chars",
                sessionId, wordCount, transcriptText.Length);

            // Enqueue the next pipeline step
            BackgroundJob.Enqueue<ContentCreationParseJob>(
                job => job.ExecuteAsync(sessionId, tenantId, CancellationToken.None));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[VideoTranscription] Unhandled error transcribing session {SessionId}",
                sessionId);

            try
            {
                session.Status = ContentCreationSessionStatus.Failed;
                session.ErrorMessage = ex.Message;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx,
                    "[VideoTranscription] Failed to save Failed status for session {SessionId}",
                    sessionId);
            }
        }
    }
}
