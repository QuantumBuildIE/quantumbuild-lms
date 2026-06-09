using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Text.Json.Serialization;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for the workflow action endpoints added in Phase 3c.2:
///   GET  /api/toolbox-talks/{id}/translations/{languageCode}/history
///   POST /api/toolbox-talks/{id}/translations/{languageCode}/accept
///
/// Each test creates a fresh ToolboxTalk so there is no ordering dependency.
/// Events are seeded with TenantId = TestTenantConstants.TenantId so they are
/// visible under the authenticated HTTP scope.
///
/// Language codes used:
///   "es" (Spanish) — history endpoint tests
///   "de" (German)  — accept endpoint tests (each test creates its own talk)
/// Neither code is used by any other test class.
/// </summary>
[Collection("Integration")]
public class ToolboxTalksControllerWorkflowActionsTests : IntegrationTestBase
{
    public ToolboxTalksControllerWorkflowActionsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── helpers ────────────────────────────────────────────────────────────────

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
            Title = "Workflow Actions Test Talk",
            Description = "Test talk for workflow actions endpoint tests",
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

    // ── history tests ─────────────────────────────────────────────────────────

    // 1 — Non-existent talk → 404
    [Fact]
    public async Task GetHistory_NonExistentTalk_Returns404()
    {
        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{Guid.NewGuid()}/translations/es/history");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 2 — Talk exists but has no events for the language → 200 with empty list
    [Fact]
    public async Task GetHistory_TalkExistsNoEventsForLanguage_Returns200EmptyList()
    {
        var talkId = await CreateTalkAsync();

        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/translations/es/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<List<WorkflowEventResponse>>();
        history.Should().NotBeNull();
        history!.Should().BeEmpty();
    }

    // 3 — Talk has events → 200 with chronologically ordered list, all fields present
    [Fact]
    public async Task GetHistory_TalkWithEvents_Returns200WithOrderedEvents()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "es", WorkflowEventTypes.TranslationStarted);
        await SeedEventAsync(talkId, "es", WorkflowEventTypes.TranslationCompleted);

        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/translations/es/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = await response.Content.ReadFromJsonAsync<List<WorkflowEventResponse>>();
        var events = history!;
        events.Should().HaveCount(2);
        events[0].EventType.Should().Be(WorkflowEventTypes.TranslationStarted);
        events[1].EventType.Should().Be(WorkflowEventTypes.TranslationCompleted);
        events.Should().BeInAscendingOrder(e => e.OccurredAt);
        events.All(e => e.OccurredAt != default).Should().BeTrue();
    }

    // 4 — Unauthenticated → 401
    [Fact]
    public async Task GetHistory_Unauthenticated_Returns401()
    {
        var response = await UnauthenticatedClient.GetAsync(
            $"/api/toolbox-talks/{Guid.NewGuid()}/translations/es/history");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── accept tests ──────────────────────────────────────────────────────────

    // 5 — Non-existent talk → 404
    [Fact]
    public async Task AcceptAsFinal_NonExistentTalk_Returns404()
    {
        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{Guid.NewGuid()}/translations/de/accept",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 6 — State is Validated → 200, AcceptedAsFinal event appended
    [Fact]
    public async Task AcceptAsFinal_FromValidatedState_Returns200AndWritesAcceptedEvent()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "de", WorkflowEventTypes.TranslationCompleted);
        await SeedEventAsync(talkId, "de", WorkflowEventTypes.ValidationCompleted);

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/translations/de/accept",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify the AcceptedAsFinal event was written by reading back the history
        var historyResponse = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/translations/de/history");
        var events = (await historyResponse.Content.ReadFromJsonAsync<List<WorkflowEventResponse>>())!;
        events.Last().EventType.Should().Be(WorkflowEventTypes.AcceptedAsFinal);
    }

    // 7 — State is ReviewerAccepted → 200
    [Fact]
    public async Task AcceptAsFinal_FromReviewerAcceptedState_Returns200()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "de", WorkflowEventTypes.TranslationCompleted);
        await SeedEventAsync(talkId, "de", WorkflowEventTypes.ValidationCompleted);
        await SeedEventAsync(talkId, "de", WorkflowEventTypes.InternalReviewSubmitted);

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/translations/de/accept",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 8 — State is ThirdPartyReviewed → 200
    [Fact]
    public async Task AcceptAsFinal_FromThirdPartyReviewedState_Returns200()
    {
        var talkId = await CreateTalkAsync();
        // ExternalReviewSubmitted → ThirdPartyReviewed (per EventTypeToState mapping)
        await SeedEventAsync(talkId, "de", WorkflowEventTypes.ExternalReviewSubmitted);

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/translations/de/accept",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // 9 — State is Initial (no events seeded) → 409 Conflict
    [Fact]
    public async Task AcceptAsFinal_FromInitialState_Returns409()
    {
        var talkId = await CreateTalkAsync();
        // No events → state is Initial, which is not an accepted source state

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/translations/de/accept",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 10 — Unauthenticated → 401
    [Fact]
    public async Task AcceptAsFinal_Unauthenticated_Returns401()
    {
        var response = await UnauthenticatedClient.PostAsync(
            $"/api/toolbox-talks/{Guid.NewGuid()}/translations/de/accept",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── local DTOs ────────────────────────────────────────────────────────────

    private record WorkflowEventResponse
    {
        public string EventType { get; init; } = string.Empty;
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TriggeredByType TriggeredByType { get; init; }
        public Guid? TriggeredByUserId { get; init; }
        public string? PayloadJson { get; init; }
        public DateTime OccurredAt { get; init; }
    }
}
