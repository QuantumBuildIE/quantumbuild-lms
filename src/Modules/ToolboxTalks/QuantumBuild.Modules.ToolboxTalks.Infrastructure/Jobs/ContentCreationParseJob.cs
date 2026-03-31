using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Background job that parses transcript text into sections via Claude AI.
/// Enqueued by VideoTranscriptionJob after transcription completes.
/// </summary>
[AutomaticRetry(Attempts = 3)]
[Queue("content-generation")]
public class ContentCreationParseJob(
    IToolboxTalksDbContext dbContext,
    IContentParserService contentParserService,
    ILogger<ContentCreationParseJob> logger)
{
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task ExecuteAsync(Guid sessionId, Guid tenantId, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[ContentCreationParse] Starting parse for session {SessionId}, tenant {TenantId}",
            sessionId, tenantId);

        var session = await dbContext.ContentCreationSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.TenantId == tenantId, cancellationToken);

        if (session is null)
        {
            logger.LogWarning(
                "[ContentCreationParse] Session {SessionId} not found for tenant {TenantId}",
                sessionId, tenantId);
            return;
        }

        if (string.IsNullOrWhiteSpace(session.TranscriptText))
        {
            logger.LogWarning(
                "[ContentCreationParse] Session {SessionId} has no TranscriptText — cannot parse",
                sessionId);
            return;
        }

        if (session.Status != ContentCreationSessionStatus.Parsing)
        {
            logger.LogWarning(
                "[ContentCreationParse] Session {SessionId} status is {Status}, expected Parsing — skipping",
                sessionId, session.Status);
            return;
        }

        try
        {
            var result = await contentParserService.ParseContentAsync(
                rawText: session.TranscriptText,
                inputModeHint: session.InputMode,
                tenantId: tenantId,
                userId: null,
                cancellationToken: cancellationToken);

            if (!result.Success)
            {
                logger.LogError(
                    "[ContentCreationParse] Parse failed for session {SessionId}: {Error}",
                    sessionId, result.ErrorMessage);
                session.Status = ContentCreationSessionStatus.Failed;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            session.ParsedSectionsJson = JsonSerializer.Serialize(result.Sections, CamelCaseOptions);
            session.OutputType = result.SuggestedOutputType;
            session.Status = ContentCreationSessionStatus.Parsed;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "[ContentCreationParse] Parse complete for session {SessionId}: {SectionCount} sections",
                sessionId, result.Sections.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[ContentCreationParse] Unhandled error parsing session {SessionId}",
                sessionId);

            try
            {
                session.Status = ContentCreationSessionStatus.Failed;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx,
                    "[ContentCreationParse] Failed to save Failed status for session {SessionId}",
                    sessionId);
            }
        }
    }
}
