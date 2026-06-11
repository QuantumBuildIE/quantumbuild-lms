using Hangfire;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ContentCreation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.ContentCreation;

/// <summary>
/// Hangfire-backed implementation of <see cref="IParseJobScheduler"/>.
/// Enqueues video transcription → parse pipeline as fire-and-forget.
/// </summary>
public class ParseJobScheduler : IParseJobScheduler
{
    public string EnqueueVideoTranscriptionJob(Guid talkId, Guid tenantId)
    {
        return BackgroundJob.Enqueue<VideoTranscriptionJobForTalk>(job =>
            job.ExecuteAsync(talkId, tenantId, CancellationToken.None));
    }
}
