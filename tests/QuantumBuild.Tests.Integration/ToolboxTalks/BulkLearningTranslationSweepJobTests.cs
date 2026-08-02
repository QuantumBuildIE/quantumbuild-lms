using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;
using QuantumBuild.Tests.Common.TestTenant;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for BulkLearningTranslationSweepJob (Bulk SOP Learnings, Chunk 2) — the
/// off-peak sweep that picks up ToolboxTalk.BulkTranslationPendingSince flags set by
/// BulkSopImportJob and enqueues MissingTranslationsJob per learning, throttled per tenant per
/// run, into the tenant's employee languages.
///
/// Dispatch strategy mirrors MissingTranslationsJobTests / BulkSopImportJobTests: the sweep job
/// itself is resolved and invoked directly rather than going through BackgroundJob.Enqueue (there
/// is no recurring-job scheduler in the test host). The MissingTranslationsJob calls it enqueues
/// are picked up for real, though — the test host runs a real Hangfire server against in-memory
/// storage (see PublishToolboxTalkTests) — so enqueued jobs are asserted via Hangfire's (in-memory,
/// test-wide) JobStorage across every state a job could be in by assertion time, filtered by talk
/// ID so accumulation across the whole test run is safe.
///
/// The base tenant's seeded employees are all PreferredLanguage="en" (TestTenantSeeder), so the
/// "no employee non-source languages" no-op case needs no special setup — it's the default state
/// unless a test seeds a non-English employee itself.
///
/// Language-code isolation: "kk" (Kazakh) and "mn" (Mongolian) are used here — unused by
/// MissingTranslationsJobTests ("be", "hy") or ToolboxTalksControllerWorkflowStateTests
/// ("lt", "lv", "ru", "af").
/// </summary>
[Collection("Integration")]
public class BulkLearningTranslationSweepJobTests : IntegrationTestBase
{
    public BulkLearningTranslationSweepJobTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task SeedEmployeeWithLanguageAsync(string languageCode)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<Employee>().Add(new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantConstants.TenantId,
            EmployeeCode = $"SWEEP-{languageCode.ToUpper()}",
            FirstName = "Sweep",
            LastName = languageCode.ToUpper(),
            PreferredLanguage = languageCode
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a minimal Draft ToolboxTalk with BulkTranslationPendingSince set, as
    /// BulkSopImportJob leaves a successfully-created bulk item. TenantId is set explicitly
    /// per Note 22 — this scope has no HTTP context to auto-stamp it.
    /// </summary>
    private async Task<Guid> SeedPendingTalkAsync(string title, DateTimeOffset pendingSince)
    {
        var talkId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ToolboxTalk>().Add(new ToolboxTalk
        {
            Id = talkId,
            TenantId = TestTenantConstants.TenantId,
            Code = talkId.ToString("N")[..8],
            Title = title,
            Status = ToolboxTalkStatus.Draft,
            Frequency = ToolboxTalkFrequency.Once,
            VideoSource = VideoSource.None,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = true,
            GenerateCertificate = false,
            BulkTranslationPendingSince = pendingSince,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return talkId;
    }

    private async Task<DateTimeOffset?> GetPendingSinceAsync(Guid talkId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var talk = await db.Set<ToolboxTalk>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.Id == talkId);
        return talk.BulkTranslationPendingSince;
    }

    /// <summary>
    /// Checks Hangfire's (in-memory, test-wide) job storage for a MissingTranslationsJob.ExecuteAsync
    /// call whose first argument is talkId. The test host registers a real Hangfire server against
    /// the in-memory storage (see PublishToolboxTalkTests), so an enqueued job is typically picked
    /// up and moved to Processing/Succeeded/Failed within milliseconds — this checks every state the
    /// job could be in, not just Enqueued, or a race with the real server produces a false negative
    /// (confirmed while writing this test: the real translation call fails fast in the test host
    /// with no network access, so the job is very often already in Failed by assertion time).
    /// </summary>
    private static bool MissingTranslationsJobEnqueuedFor(Guid talkId)
    {
        var api = JobStorage.Current.GetMonitoringApi();

        bool Matches(Hangfire.Common.Job? job) =>
            job?.Method.Name == nameof(MissingTranslationsJob.ExecuteAsync)
            && job.Type == typeof(MissingTranslationsJob)
            && job.Args.Count > 0
            && job.Args[0] is Guid argTalkId
            && argTalkId == talkId;

        return api.EnqueuedJobs("content-generation", 0, 1000).Any(kvp => Matches(kvp.Value?.Job))
            || api.FetchedJobs("content-generation", 0, 1000).Any(kvp => Matches(kvp.Value?.Job))
            || api.ProcessingJobs(0, 1000).Any(kvp => Matches(kvp.Value?.Job))
            || api.SucceededJobs(0, 1000).Any(kvp => Matches(kvp.Value?.Job))
            || api.FailedJobs(0, 1000).Any(kvp => Matches(kvp.Value?.Job));
    }

    private async Task RunSweepAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<BulkLearningTranslationSweepJob>(scope.ServiceProvider);
        await job.ExecuteAsync(CancellationToken.None);
    }

    // ── tests ──────────────────────────────────────────────────────────────────

    // A pending bulk-created Draft learning, tenant has an employee needing "kk" — the sweep
    // enqueues MissingTranslationsJob for it and clears the pending flag.
    [Fact]
    public async Task PendingLearning_SweepEnqueuesMissingTranslationsJob_AndClearsFlag()
    {
        await SeedEmployeeWithLanguageAsync("kk");
        var talkId = await SeedPendingTalkAsync("Sweep Single Talk", DateTimeOffset.UtcNow.AddHours(-2));

        await RunSweepAsync();

        MissingTranslationsJobEnqueuedFor(talkId).Should().BeTrue(
            "the sweep should enqueue MissingTranslationsJob for the one pending learning");

        var pendingSince = await GetPendingSinceAsync(talkId);
        pendingSince.Should().BeNull(
            "the pending flag must be cleared once the sweep enqueues the translation job for it");
    }

    // A batch of 12 pending learnings — one more than the sweep's per-tenant-per-run cap (10) —
    // spreads across two runs: the 10 oldest are swept first (FIFO by BulkTranslationPendingSince),
    // the remaining 2 stay flagged and are picked up on the next run.
    [Fact]
    public async Task BatchLargerThanCap_SweepThrottlesAcrossRuns()
    {
        await SeedEmployeeWithLanguageAsync("mn");

        var baseline = DateTimeOffset.UtcNow.AddDays(-1);
        var talkIds = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            // Strictly increasing timestamps so ordering (and therefore which 10 are swept
            // first) is deterministic.
            var talkId = await SeedPendingTalkAsync($"Sweep Batch Talk {i}", baseline.AddMinutes(i));
            talkIds.Add(talkId);
        }

        await RunSweepAsync();

        var oldestTen = talkIds.Take(10).ToList();
        var youngestTwo = talkIds.Skip(10).ToList();

        foreach (var talkId in oldestTen)
        {
            MissingTranslationsJobEnqueuedFor(talkId).Should().BeTrue(
                $"talk {talkId} is among the 10 oldest pending learnings and should be swept in run 1");
            (await GetPendingSinceAsync(talkId)).Should().BeNull();
        }

        foreach (var talkId in youngestTwo)
        {
            MissingTranslationsJobEnqueuedFor(talkId).Should().BeFalse(
                $"talk {talkId} is beyond the per-run cap and should not be swept in run 1");
            (await GetPendingSinceAsync(talkId)).Should().NotBeNull(
                "learnings beyond the cap must stay flagged so a later run picks them up");
        }

        // Run 2 — the throttle cap (10) comfortably covers the 2 remaining, clearing the batch.
        await RunSweepAsync();

        foreach (var talkId in youngestTwo)
        {
            MissingTranslationsJobEnqueuedFor(talkId).Should().BeTrue(
                $"talk {talkId} should be swept on run 2, now that run 1's items are cleared");
            (await GetPendingSinceAsync(talkId)).Should().BeNull();
        }
    }

    // No employee at the base tenant prefers a non-English language (TestTenantSeeder seeds only
    // "en"), so a pending learning is a clean no-op: nothing enqueued, flag left set so the
    // learning is picked up automatically once a qualifying employee exists.
    [Fact]
    public async Task NoNonSourceEmployeeLanguages_CleanNoOp_FlagRemainsSet()
    {
        var talkId = await SeedPendingTalkAsync("Sweep No-Op Talk", DateTimeOffset.UtcNow.AddHours(-1));

        await RunSweepAsync();

        MissingTranslationsJobEnqueuedFor(talkId).Should().BeFalse(
            "a tenant with no non-English employee languages has nothing to translate into");

        var pendingSince = await GetPendingSinceAsync(talkId);
        pendingSince.Should().NotBeNull(
            "the flag must be left set (not cleared/dropped) so this learning is swept later " +
            "if a qualifying employee is added");
    }

    /// <summary>
    /// Returns all WorkflowEvents for the given talk + language pair — same helper shape as
    /// MissingTranslationsJobTests.
    /// </summary>
    private async Task<List<WorkflowEvent>> GetEventsAsync(Guid talkId, string languageCode)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.TargetEntityId == talkId
                     && e.TargetEntitySubKey == languageCode
                     && !e.IsDeleted)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync();
    }

    // Proves the sweep actually drives the real translation pipeline for a DRAFT bulk-created
    // learning end to end (not just that a job got queued): MissingTranslationsJob has no Status
    // filter (confirmed in docs/translation-scan-behaviour-recon.md §2), so a Draft talk must
    // reach GenerateContentTranslationsCommandHandler and record a system-triggered
    // TranslationStarted event, exactly as MissingTranslationsJobTests proves for its direct
    // (non-bulk) invocation path. The real Hangfire server in the test host (see
    // PublishToolboxTalkTests) picks the enqueued job up asynchronously, so this polls briefly
    // rather than asserting immediately after RunSweepAsync returns. This is as far as an
    // automated, network-isolated test host can verify — the actual translated text is produced
    // by a real Claude/DeepL HTTP call that this sandbox cannot make (confirmed while building
    // this test: the call fails fast with TaskCanceledException, no outbound network access),
    // matching the same limitation MissingTranslationsJobTests and
    // GenerateContentTranslationsCommandHandlerTests already document for this exact call chain.
    [Fact]
    public async Task PendingDraftLearning_SweepDrivesRealTranslationPipeline_WritesSystemTriggeredEvent()
    {
        const string langCode = "uz"; // Uzbek — unused by any other test class in this suite
        await SeedEmployeeWithLanguageAsync(langCode);
        var talkId = await SeedPendingTalkAsync("Sweep Draft Pipeline Talk", DateTimeOffset.UtcNow.AddHours(-3));

        await RunSweepAsync();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        List<WorkflowEvent> events = [];
        while (DateTime.UtcNow < deadline)
        {
            events = await GetEventsAsync(talkId, langCode);
            if (events.Any(e => e.EventType == WorkflowEventTypes.TranslationStarted))
                break;
            await Task.Delay(200);
        }

        events.Should().Contain(e => e.EventType == WorkflowEventTypes.TranslationStarted,
            "the swept Draft learning must reach GenerateContentTranslationsCommandHandler, " +
            "which writes TranslationStarted before attempting the actual translation call");

        var started = events.First(e => e.EventType == WorkflowEventTypes.TranslationStarted);
        started.TriggeredByType.Should().Be(TriggeredByType.System,
            "the sweep-enqueued job has no user identity, so this must be recorded as System-triggered");
    }
}
