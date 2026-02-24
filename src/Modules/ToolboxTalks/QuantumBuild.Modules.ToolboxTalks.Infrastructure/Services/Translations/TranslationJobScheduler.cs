using Hangfire;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Translations;

/// <summary>
/// Hangfire-backed implementation of <see cref="ITranslationJobScheduler"/>.
/// Enqueues translation jobs as fire-and-forget so callers return immediately.
/// </summary>
public class TranslationJobScheduler : ITranslationJobScheduler
{
    public string EnqueueMissingTranslationsJob(Guid toolboxTalkId, Guid tenantId)
    {
        return BackgroundJob.Enqueue<MissingTranslationsJob>(job =>
            job.ExecuteAsync(toolboxTalkId, tenantId, null, CancellationToken.None));
    }
}
