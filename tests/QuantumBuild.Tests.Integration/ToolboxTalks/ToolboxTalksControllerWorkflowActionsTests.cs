using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using System.Text.Json;
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

    /// <summary>Seeds a ToolboxTalkTranslation for (talkId, languageCode) with the given JSON sections.</summary>
    private async Task SeedTranslationAsync(Guid talkId, string languageCode, string translatedSectionsJson)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ToolboxTalkTranslation>().Add(new ToolboxTalkTranslation
        {
            TenantId = TestTenantConstants.TenantId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            TranslatedTitle = $"Test translation ({languageCode})",
            TranslatedSections = translatedSectionsJson,
            TranslatedAt = DateTime.UtcNow,
            TranslationProvider = "test"
        });
        await db.SaveChangesAsync();
    }

    private sealed class TranslatedSectionSnapshot
    {
        public Guid SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
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

    // 8 — State is ThirdPartyReviewed → 200. Under auto-apply (Option A — see
    //     docs/external-review-auto-apply-recon.md), SubmitExternalReview already merged the
    //     reviewer's edits into TranslatedSections and stamped provenance before Accept is ever
    //     called; AcceptAsFinal only closes the state and must not touch that data.
    [Fact]
    public async Task AcceptAsFinal_FromThirdPartyReviewedState_Returns200()
    {
        var talkId = await CreateTalkAsync();
        const string lang = "de";
        var sectionId = Guid.NewGuid();

        await SeedTranslationAsync(talkId, lang, JsonSerializer.Serialize(new[]
        {
            new { SectionId = sectionId, Title = "Section 1", Content = "Original AI translation" }
        }));

        // Reach ReviewerAccepted so InitiateExternalReview is legal
        await SeedEventAsync(talkId, lang, WorkflowEventTypes.TranslationCompleted);
        await SeedEventAsync(talkId, lang, WorkflowEventTypes.ValidationCompleted);
        await SeedEventAsync(talkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        string rawToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

            // explicitTenantId required: this scope has no HttpContext, so ICurrentUserService
            // would otherwise resolve Guid.Empty and miss the TestTenantConstants.TenantId events
            // seeded above (see CLAUDE.md Note 22 / the Hangfire-context pattern).
            var initiate = await service.InitiateExternalReview(talkId, lang, "reviewer@example.com",
                explicitTenantId: TestTenantConstants.TenantId);
            initiate.Success.Should().BeTrue();
            rawToken = initiate.Data!.Token;

            var editsJson = JsonSerializer.Serialize(new[]
            {
                new { SectionIndex = 0, TranslatedText = "Reviewer edited translation" }
            });
            var submit = await service.SubmitExternalReview(rawToken, accepted: true, editedContent: editsJson);
            submit.Success.Should().BeTrue();
        }

        // Auto-apply already happened before Accept is even called
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var translation = await db.Set<ToolboxTalkTranslation>()
                .IgnoreQueryFilters()
                .FirstAsync(t => t.ToolboxTalkId == talkId && t.LanguageCode == lang && !t.IsDeleted);
            var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation.TranslatedSections);
            sections![0].Content.Should().Be("Reviewer edited translation");
            translation.LastExternalReviewedAt.Should().NotBeNull();
            translation.LastExternalReviewedBy.Should().Be("reviewer@example.com");
        }

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/translations/{lang}/accept",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Accept must not alter the already-applied edits — it only closes the state.
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var translation = await db.Set<ToolboxTalkTranslation>()
                .IgnoreQueryFilters()
                .FirstAsync(t => t.ToolboxTalkId == talkId && t.LanguageCode == lang && !t.IsDeleted);
            var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation.TranslatedSections);
            sections![0].Content.Should().Be("Reviewer edited translation");
        }
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

    // ── cancel-external-review tests ──────────────────────────────────────────

    private async Task SeedInvitationAsync(Guid talkId, string languageCode)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Set<ExternalParticipantInvitation>().Add(new ExternalParticipantInvitation
        {
            TenantId = TestTenantConstants.TenantId,
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            InvitedEmail = "reviewer@example.com",
            TokenHash = "testhash_" + talkId.ToString("N"),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Status = InvitationStatus.Pending,
            RequesterUserId = Guid.NewGuid(),
            InvitedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // 11 — Non-existent talk → 404
    [Fact]
    public async Task CancelExternalReview_NonExistentTalk_Returns404()
    {
        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{Guid.NewGuid()}/translations/fr/cancel-external-review",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 12 — State is AwaitingThirdParty with a Pending invitation → 200, ExternalReviewCancelled event written, state reverts to ReviewerAccepted
    [Fact]
    public async Task CancelExternalReview_FromAwaitingThirdPartyState_Returns200AndWritesCancelledEvent()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "fr", WorkflowEventTypes.TranslationCompleted);
        await SeedEventAsync(talkId, "fr", WorkflowEventTypes.ValidationCompleted);
        await SeedEventAsync(talkId, "fr", WorkflowEventTypes.InternalReviewSubmitted);
        await SeedEventAsync(talkId, "fr", WorkflowEventTypes.ExternalReviewInitiated);
        await SeedInvitationAsync(talkId, "fr");

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/translations/fr/cancel-external-review",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify ExternalReviewCancelled was appended as the last event (which also implies
        // state reverted to ReviewerAccepted per the EventTypeToState mapping)
        var historyResponse = await AdminClient.GetAsync(
            $"/api/toolbox-talks/{talkId}/translations/fr/history");
        var events = (await historyResponse.Content.ReadFromJsonAsync<List<WorkflowEventResponse>>())!;
        events.Last().EventType.Should().Be(WorkflowEventTypes.ExternalReviewCancelled);
    }

    // 13 — State is Initial (no events) → 409 Conflict
    [Fact]
    public async Task CancelExternalReview_FromInitialState_Returns409()
    {
        var talkId = await CreateTalkAsync();

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/translations/fr/cancel-external-review",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 14 — State is ReviewerAccepted (no invitation sent yet) → 409 Conflict
    [Fact]
    public async Task CancelExternalReview_FromReviewerAcceptedState_Returns409()
    {
        var talkId = await CreateTalkAsync();
        await SeedEventAsync(talkId, "fr", WorkflowEventTypes.InternalReviewSubmitted);

        var response = await AdminClient.PostAsync(
            $"/api/toolbox-talks/{talkId}/translations/fr/cancel-external-review",
            new StringContent(string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 15 — Unauthenticated → 401
    [Fact]
    public async Task CancelExternalReview_Unauthenticated_Returns401()
    {
        var response = await UnauthenticatedClient.PostAsync(
            $"/api/toolbox-talks/{Guid.NewGuid()}/translations/fr/cancel-external-review",
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
