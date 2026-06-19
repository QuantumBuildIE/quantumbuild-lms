using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

/// <summary>
/// Parses the video transcript stored on a new-wizard ToolboxTalk into
/// ToolboxTalkSection rows via Claude AI.
/// Enqueued by VideoTranscriptionJobForTalk after transcription completes.
/// Analogous to ContentCreationParseJob (session-based) but targets the talk row directly.
/// </summary>
[AutomaticRetry(Attempts = 3)]
[Queue("content-generation")]
public class ContentCreationParseJobForTalk(
    IToolboxTalksDbContext dbContext,
    IContentParserService contentParserService,
    ILogger<ContentCreationParseJobForTalk> logger)
{
    public async Task ExecuteAsync(Guid talkId, Guid tenantId, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[ContentCreationParseForTalk] Starting parse for talk {TalkId}, tenant {TenantId}",
            talkId, tenantId);

        var talk = await dbContext.ToolboxTalks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == talkId && t.TenantId == tenantId, cancellationToken);

        if (talk is null)
        {
            logger.LogWarning(
                "[ContentCreationParseForTalk] Talk {TalkId} not found for tenant {TenantId}",
                talkId, tenantId);
            return;
        }

        if (talk.IsDeleted)
        {
            logger.LogInformation(
                "[ContentCreationParseForTalk] Talk {TalkId} has been deleted — skipping",
                talkId);
            return;
        }

        if (talk.Status != ToolboxTalkStatus.Processing)
        {
            logger.LogWarning(
                "[ContentCreationParseForTalk] Talk {TalkId} status is {Status}, expected Processing — skipping",
                talkId, talk.Status);
            return;
        }

        if (string.IsNullOrWhiteSpace(talk.ExtractedVideoTranscript))
        {
            logger.LogWarning(
                "[ContentCreationParseForTalk] Talk {TalkId} has no transcript — reverting to Draft",
                talkId);
            talk.Status = ToolboxTalkStatus.Draft;
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var result = await contentParserService.ParseContentAsync(
                rawText: talk.ExtractedVideoTranscript,
                inputModeHint: InputMode.Video,
                tenantId: tenantId,
                userId: null,
                preserveSourceWording: talk.PreserveSourceWording,
                cancellationToken: cancellationToken);

            if (!result.Success)
            {
                logger.LogError(
                    "[ContentCreationParseForTalk] Parse failed for talk {TalkId}: {Error}",
                    talkId, result.ErrorMessage);
                talk.Status = ToolboxTalkStatus.Draft;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Soft-delete any existing sections (e.g. from a prior parse attempt)
            var existing = await dbContext.ToolboxTalkSections
                .Where(s => s.ToolboxTalkId == talkId && !s.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var s in existing)
                s.IsDeleted = true;

            var sectionNumber = 1;
            foreach (var parsed in result.Sections.OrderBy(s => s.SuggestedOrder))
            {
                dbContext.ToolboxTalkSections.Add(new ToolboxTalkSection
                {
                    Id = Guid.NewGuid(),
                    ToolboxTalkId = talkId,
                    SectionNumber = sectionNumber++,
                    Title = parsed.Title,
                    Content = parsed.Content,
                    RequiresAcknowledgment = true,
                    Source = ContentSource.Video,
                });
            }

            talk.GeneratedFromVideo = true;
            talk.Status = ToolboxTalkStatus.Draft;
            talk.LastEditedStep = 2;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "[ContentCreationParseForTalk] Parse complete for talk {TalkId}: {SectionCount} sections",
                talkId, result.Sections.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[ContentCreationParseForTalk] Unhandled error parsing talk {TalkId}", talkId);
            try
            {
                talk.Status = ToolboxTalkStatus.Draft;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception saveEx)
            {
                logger.LogError(saveEx,
                    "[ContentCreationParseForTalk] Failed to revert status for talk {TalkId}", talkId);
            }
        }
    }
}
