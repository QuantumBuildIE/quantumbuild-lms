using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateContentTranslations;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for the workflow guard (StartTranslation) wired into
/// GenerateContentTranslationsCommandHandler (Phase 3b.1 §1.1–1.4).
///
/// Dispatch strategy: MediatR ISender from a Factory scope (no HTTP context).
/// The handler uses IgnoreQueryFilters() + explicit TenantId for the ToolboxTalk lookup
/// so HTTP context is not required.
///
/// Workflow events are stamped with TenantId = Guid.Empty (ICurrentUserService.TenantId
/// in a non-HTTP scope). Assertions use IgnoreQueryFilters() to find them regardless.
///
/// Translation outcome is NOT tested here — the real IContentTranslationService is registered
/// in the test host and will fail gracefully (no external API config) after the guard passes.
/// The tests only verify guard decisions: which events are written (or not written).
///
/// Isolation: each test uses a distinct language code so events written by one test are never
/// visible to another test's assertion:
///   Test 1 (InitialLanguage)                        → "fr"
///   Test 2 (AcceptedWithoutConfirmOverwrite)         → "de"
///   Test 3 (AcceptedWithConfirmOverwrite)            → "it"
///   Test 4 (MixedLanguages blocked + unblocked)     → "de" (blocked) + "ms" (unblocked)
///
/// "fr" and "de" are safe because TranslationWorkflowServiceTests only reads them (GetState /
/// GetHistory) — it never writes any WorkflowEvent for those codes.
/// "ms" (Malay) is completely untouched by TranslationWorkflowServiceTests.
/// "es" and "pl" were previously used but are contaminated: TranslationWorkflowServiceTests
/// writes TranslationStarted for "es" and ExternalReviewInitiated for "pl".
/// Helper methods use self-contained scopes (not GetDbContext()) to avoid leaking connections.
/// </summary>
[Collection("Integration")]
public class GenerateContentTranslationsCommandHandlerTests : IntegrationTestBase
{
    private static readonly Guid TalkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk;

    public GenerateContentTranslationsCommandHandlerTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a WorkflowEvent directly to pre-condition the workflow state.
    /// TenantId is left unset so it is auto-stamped to Guid.Empty — consistent with
    /// how the service writes events from a non-HTTP scope (test environment).
    /// Uses a self-contained scope to avoid leaking DB connections.
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
    /// Events written in non-HTTP scope have TenantId = Guid.Empty; IgnoreQueryFilters sees all.
    /// Uses a self-contained scope to avoid leaking DB connections.
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

    /// <summary>
    /// Dispatches GenerateContentTranslationsCommand via MediatR from a non-HTTP scope.
    /// Uses TestTenantConstants.TenantId as the explicit tenant (handler filters by this).
    /// </summary>
    private async Task<GenerateContentTranslationsResult> DispatchAsync(
        List<string> languages,
        bool confirmOverwrite = false,
        string? sectorKey = null)
    {
        using var scope = Factory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GenerateContentTranslationsCommand
        {
            ToolboxTalkId = TalkId,
            TenantId = TestTenantConstants.TenantId,
            TargetLanguages = languages,
            ConfirmOverwrite = confirmOverwrite,
            SectorKey = sectorKey
        });
    }

    // ── tests ──────────────────────────────────────────────────────────────────

    // 1 — Initial language ("fr"), no prior events → guard allows → TranslationStarted written
    [Fact]
    public async Task InitialLanguage_GuardPasses_WritesTranslationStartedEvent()
    {
        // Act
        var result = await DispatchAsync(["French"]);

        // Assert — command itself succeeded (even if translation failed due to no API config)
        result.Should().NotBeNull();

        // Guard passed → TranslationStarted event must have been written for "fr"
        var events = await GetEventsAsync(TalkId, "fr");
        events.Should().Contain(e => e.EventType == WorkflowEventTypes.TranslationStarted,
            "guard should have allowed the Initial state and written TranslationStarted");
        // TranslationCompleted is not asserted here: the real translation service has no API config
        // in the test environment so TranslateForLanguageAsync always returns Success=false, meaning
        // RecordTranslationCompleted is never reached. RecordTranslationCompleted is tested directly
        // in TranslationWorkflowServiceTests (tests 27-29).
    }

    // 2 — Accepted language ("de"), ConfirmOverwrite=false → guard blocks → no TranslationStarted written
    [Fact]
    public async Task AcceptedLanguage_WithoutConfirmOverwrite_GuardBlocks_NoEventWritten()
    {
        // Arrange — put "de" (German) into Accepted state. "de" is safe: TranslationWorkflowServiceTests
        // only calls GetHistory("de") — no events written — so no cross-class contamination.
        await SeedEventAsync(TalkId, "de", WorkflowEventTypes.AcceptedAsFinal);

        // Act
        var result = await DispatchAsync(["German"], confirmOverwrite: false);

        // Assert — command completes (not thrown), but "German" entry is marked failed
        result.Should().NotBeNull();
        var deResult = result.LanguageResults.FirstOrDefault(r => r.LanguageCode == "de");
        deResult.Should().NotBeNull("German should appear in LanguageResults even when blocked");
        deResult!.Success.Should().BeFalse("guard should have blocked the Accepted language");
        deResult.ErrorMessage.Should().NotBeNullOrEmpty();
        deResult.ErrorMessage.Should().Contain("Confirmation",
            "the error message should hint that confirmation is required");

        // No TranslationStarted must have been written (only the seeded AcceptedAsFinal exists)
        var events = await GetEventsAsync(TalkId, "de");
        events.Should().NotContain(e => e.EventType == WorkflowEventTypes.TranslationStarted,
            "guard should have prevented TranslationStarted from being written");
    }

    // 3 — Accepted language ("it"), ConfirmOverwrite=true → guard allows → TranslationStarted written
    [Fact]
    public async Task AcceptedLanguage_WithConfirmOverwrite_GuardPasses_WritesTranslationStartedEvent()
    {
        // Arrange — put "it" (Italian) into Accepted state; uses "it" not "fr" or "es"
        // to avoid contamination from other tests.
        await SeedEventAsync(TalkId, "it", WorkflowEventTypes.AcceptedAsFinal);

        // Act
        var result = await DispatchAsync(["Italian"], confirmOverwrite: true);

        // Assert — guard allowed it → TranslationStarted written
        result.Should().NotBeNull();

        var events = await GetEventsAsync(TalkId, "it");
        events.Should().Contain(e => e.EventType == WorkflowEventTypes.TranslationStarted,
            "confirmOverwrite=true should have allowed overwriting the Accepted state");
    }

    // 4 — Mixed batch: one blocked ("de"), one initial ("ms") → blocked language does not prevent the other
    [Fact]
    public async Task MixedLanguages_BlockedDoesNotPreventOtherLanguage()
    {
        // Arrange — put "de" (German) into Accepted state; "ms" (Malay) stays Initial.
        // "ms" is completely untouched by TranslationWorkflowServiceTests so it has no
        // pre-existing events that would put it in a blocking state.
        await SeedEventAsync(TalkId, "de", WorkflowEventTypes.AcceptedAsFinal);

        // Act — dispatch for both German (blocked) and Malay (unblocked), no confirmOverwrite
        var result = await DispatchAsync(["German", "Malay"], confirmOverwrite: false);

        // Assert — both languages appear in results
        result.Should().NotBeNull();

        var deResult = result.LanguageResults.FirstOrDefault(r => r.LanguageCode == "de");
        deResult.Should().NotBeNull();
        deResult!.Success.Should().BeFalse("German should be blocked (Accepted without confirmOverwrite)");

        // German guard blocked → no TranslationStarted for "de"
        var deEvents = await GetEventsAsync(TalkId, "de");
        deEvents.Should().NotContain(e => e.EventType == WorkflowEventTypes.TranslationStarted,
            "the blocked language should not have a TranslationStarted event");

        // Malay guard passed → TranslationStarted written for "ms"
        var msEvents = await GetEventsAsync(TalkId, "ms");
        msEvents.Should().Contain(e => e.EventType == WorkflowEventTypes.TranslationStarted,
            "the unblocked language should have a TranslationStarted event despite the other being blocked");
    }
}
