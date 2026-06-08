using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;
using QuantumBuild.Tests.Common.TestTenant;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests verifying that MissingTranslationsJob emits WorkflowEvents
/// with TriggeredByType=System (Phase 3b.2).
///
/// Dispatch strategy: resolve MissingTranslationsJob via ActivatorUtilities so all
/// real dependencies (SignalR hub context, ISender, etc.) are injected from the
/// test host — same pattern as Hangfire's AspNetCoreJobActivator.
///
/// Isolation: each test inserts its own Employee with a unique language code ("be",
/// "hy") so the job's employee-language query only sees the intended language(s).
/// BasicTalk is used as the target talk (already seeded with TenantId = TestTenantId).
///
/// Language-code isolation:
///   Test 1 (MissingLanguage_WritesSystemTriggered)  → "be" (Belarusian)
///   Test 2 (AcceptedLanguage_GuardBlocks)           → "hy" (Armenian)
///
/// Both codes are unused by TranslationWorkflowServiceTests and
/// GenerateContentTranslationsCommandHandlerTests.
/// </summary>
[Collection("Integration")]
public class MissingTranslationsJobTests : IntegrationTestBase
{
    private static readonly Guid TalkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk;

    public MissingTranslationsJobTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a minimal Employee with the given PreferredLanguage into the test tenant.
    /// Uses a self-contained scope to avoid leaking DB connections.
    /// </summary>
    private async Task SeedEmployeeWithLanguageAsync(string languageCode)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<Employee>().Add(new Employee
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantConstants.TenantId,
            EmployeeCode = $"JOB-{languageCode.ToUpper()}",
            FirstName = "Job",
            LastName = languageCode.ToUpper(),
            PreferredLanguage = languageCode
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Inserts a WorkflowEvent directly to pre-condition the workflow state for a language.
    /// TenantId auto-stamps to Guid.Empty (non-HTTP scope) — consistent with the service.
    /// </summary>
    private async Task SeedEventAsync(Guid talkId, string languageCode, string eventType)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<WorkflowEvent>().Add(new WorkflowEvent
        {
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            EventType = eventType,
            TriggeredByType = TriggeredByType.User,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Returns all WorkflowEvents for the given talk + language pair, bypassing query filters.
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

    // ── tests ──────────────────────────────────────────────────────────────────

    // 1 — Employee has language "be" with no translation → job dispatches command →
    //     TranslationStarted written with TriggeredByType=System
    [Fact]
    public async Task MissingLanguage_JobDispatchesCommand_WritesSystemTriggeredTranslationStarted()
    {
        const string langCode = "be"; // Belarusian — unique to this test class
        await SeedEmployeeWithLanguageAsync(langCode);

        using var scope = Factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<MissingTranslationsJob>(scope.ServiceProvider);

        await job.ExecuteAsync(TalkId, TestTenantConstants.TenantId, connectionId: null);

        var events = await GetEventsAsync(TalkId, langCode);
        events.Should().Contain(e => e.EventType == WorkflowEventTypes.TranslationStarted,
            "the job should have dispatched GenerateContentTranslationsCommand which calls StartTranslation");

        var started = events.First(e => e.EventType == WorkflowEventTypes.TranslationStarted);
        started.TriggeredByType.Should().Be(TriggeredByType.System,
            "job-dispatched translations must record System as the trigger type");
        started.TriggeredByUserId.Should().BeNull(
            "no user identity is available in a background job context");
    }

    // 2 — Employee has language "hy"; workflow state is Accepted → StartTranslation guard
    //     blocks the command (ConfirmOverwrite=false) → no TranslationStarted written
    [Fact]
    public async Task AcceptedLanguage_GuardBlocks_NoTranslationStartedWritten()
    {
        const string langCode = "hy"; // Armenian — unique to this test class
        await SeedEmployeeWithLanguageAsync(langCode);
        // Seed Accepted workflow state (no ToolboxTalkTranslation row → job will attempt to translate)
        await SeedEventAsync(TalkId, langCode, WorkflowEventTypes.AcceptedAsFinal);

        using var scope = Factory.Services.CreateScope();
        var job = ActivatorUtilities.CreateInstance<MissingTranslationsJob>(scope.ServiceProvider);

        await job.ExecuteAsync(TalkId, TestTenantConstants.TenantId, connectionId: null);

        var events = await GetEventsAsync(TalkId, langCode);
        events.Should().NotContain(e => e.EventType == WorkflowEventTypes.TranslationStarted,
            "StartTranslation guard blocks overwriting an Accepted language when ConfirmOverwrite=false");
        events.Should().NotContain(e => e.EventType == WorkflowEventTypes.TranslationCompleted,
            "if StartTranslation is blocked no completion event can be written");
    }
}
