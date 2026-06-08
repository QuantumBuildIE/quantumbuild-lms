using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for GET /api/toolbox-talks/{id}/translations/workflow-state (Phase 3b.3).
///
/// Each test that needs a ToolboxTalk creates a fresh one via CreateTalkAsync() so there is
/// no ordering dependency between tests within the class.
///
/// Events are seeded with TenantId = TestTenantConstants.TenantId so they are visible when
/// the service is called from the HTTP scope (ICurrentUserService.TenantId = TenantId).
/// This differs from TranslationWorkflowServiceTests / GenerateContentTranslationsCommandHandlerTests,
/// which call the service from a scope without HTTP context (TenantId = Guid.Empty) and can
/// therefore leave TenantId unset on seeded events.
///
/// Language codes used:
///   "lt" (Lithuanian) — Initial state (no events)
///   "lv" (Latvian)    — AIGenerated state (TranslationCompleted event)
/// Neither code is used by any other test class.
/// </summary>
[Collection("Integration")]
public class ToolboxTalksControllerWorkflowStateTests : IntegrationTestBase
{
    public ToolboxTalksControllerWorkflowStateTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a minimal ToolboxTalk directly and returns its ID.
    /// TenantId is set explicitly per Note 22 — the HTTP scope's auto-stamp is not available here.
    /// Uses a self-contained scope to avoid leaking DB connections.
    /// </summary>
    private async Task<Guid> CreateTalkAsync()
    {
        var talkId = Guid.NewGuid();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ToolboxTalk>().Add(new ToolboxTalk
        {
            Id = talkId,
            TenantId = TestTenantConstants.TenantId,
            Code = talkId.ToString("N")[..8],
            Title = "Workflow State Test Talk",
            Description = "Test talk for workflow state endpoint tests",
            Frequency = ToolboxTalkFrequency.Once,
            VideoSource = VideoSource.None,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = true,
            GenerateCertificate = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return talkId;
    }

    /// <summary>
    /// Seeds a ToolboxTalkTranslation row for the given talk and language code.
    /// TenantId is set explicitly per Note 22.
    /// Uses a self-contained scope to avoid leaking DB connections.
    /// </summary>
    private async Task SeedTranslationAsync(Guid talkId, string languageCode)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ToolboxTalkTranslation>().Add(new ToolboxTalkTranslation
        {
            TenantId = TestTenantConstants.TenantId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            TranslatedTitle = $"Test Title ({languageCode})",
            TranslatedSections = "[]",
            EmailSubject = $"Test Subject ({languageCode})",
            EmailBody = $"Test Body ({languageCode})",
            TranslatedAt = DateTime.UtcNow,
            TranslationProvider = "TestProvider",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Inserts a WorkflowEvent row directly to pre-condition the workflow state.
    /// TenantId is set explicitly so events are visible from the HTTP scope
    /// (ICurrentUserService.TenantId = TestTenantConstants.TenantId).
    /// Uses a self-contained scope to avoid leaking DB connections.
    /// </summary>
    private async Task SeedEventAsync(Guid talkId, string languageCode, string eventType)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<WorkflowEvent>().Add(new WorkflowEvent
        {
            TenantId = TestTenantConstants.TenantId,
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            EventType = eventType,
            TriggeredByType = TriggeredByType.User,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    // 1 — Non-existent talk → 404
    [Fact]
    public async Task GetWorkflowState_NonExistentTalk_Returns404()
    {
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{Guid.NewGuid()}/translations/workflow-state");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 2 — Talk with no translations → 200 empty list
    [Fact]
    public async Task GetWorkflowState_TalkWithNoTranslations_ReturnsEmptyList()
    {
        var talkId = await CreateTalkAsync();

        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/translations/workflow-state");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<WorkflowStateResponse>>();
        result.Should().NotBeNull();
        result!.Should().BeEmpty();
    }

    // 3 — Talk with two translations in different states → correct State per language
    [Fact]
    public async Task GetWorkflowState_TalkWithTwoTranslations_ReturnsCorrectStates()
    {
        // "lt" → no events        → Initial
        // "lv" → TranslationCompleted event → AIGenerated
        var talkId = await CreateTalkAsync();
        await SeedTranslationAsync(talkId, "lt");
        await SeedTranslationAsync(talkId, "lv");
        await SeedEventAsync(talkId, "lv", WorkflowEventTypes.TranslationCompleted);

        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/translations/workflow-state");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<List<WorkflowStateResponse>>();
        result.Should().NotBeNull();
        result!.Should().HaveCount(2);

        var ltState = result!.Single(s => s.LanguageCode == "lt");
        ltState.State.Should().Be(TranslationWorkflowState.Initial);

        var lvState = result!.Single(s => s.LanguageCode == "lv");
        lvState.State.Should().Be(TranslationWorkflowState.AIGenerated);
    }

    // 4 — No auth → 401
    [Fact]
    public async Task GetWorkflowState_Unauthenticated_Returns401()
    {
        var response = await UnauthenticatedClient.GetAsync(
            $"/api/toolbox-talks/{Guid.NewGuid()}/translations/workflow-state");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── local DTO ────────────────────────────────────────────────────────────

    private record WorkflowStateResponse
    {
        public Guid TalkId { get; init; }
        public string LanguageCode { get; init; } = string.Empty;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TranslationWorkflowState State { get; init; }
        public string? LastEventType { get; init; }
        public DateTime? LastEventAt { get; init; }
        public string? TranslatedTitle { get; init; }
        public DateTime? TranslatedAt { get; init; }
        public bool NeedsRevalidation { get; init; }
    }
}
