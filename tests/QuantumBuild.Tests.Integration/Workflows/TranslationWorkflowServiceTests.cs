using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.Workflows;

[Collection("Integration")]
public class TranslationWorkflowServiceTests : IntegrationTestBase
{
    private static readonly Guid TalkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk;

    public TranslationWorkflowServiceTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts a WorkflowEvent row directly, bypassing the service, to pre-condition state for guard tests.
    /// TenantId is left unset so it is auto-stamped to Guid.Empty — consistent with how the service
    /// writes events when resolved in a DI scope with no HTTP context (test environment).
    /// </summary>
    private async Task SeedEventAsync(Guid talkId, string languageCode, string eventType)
    {
        var db = GetDbContext();
        db.Set<WorkflowEvent>().Add(new WorkflowEvent
        {
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            EventType = eventType,
            TriggeredByType = TriggeredByType.User,
            TriggeredByUserId = null,
            PayloadJson = null,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Inserts an ExternalParticipantInvitation directly, returning the raw token.
    /// Used to set up SubmitExternalReview tests where the preceding InitiateExternalReview guard
    /// would otherwise block valid state-machine test scenarios.
    /// </summary>
    private async Task<string> SeedInvitationAsync(Guid talkId, string languageCode)
    {
        var rawToken = Guid.NewGuid().ToString("N");
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        var db = GetDbContext();
        db.Set<ExternalParticipantInvitation>().Add(new ExternalParticipantInvitation
        {
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            InvitedEmail = "test@example.com",
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Status = InvitationStatus.Pending,
            RequesterUserId = Guid.Empty,
            InvitedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        return rawToken;
    }

    // ── 1-3, 10 — unchanged tests ─────────────────────────────────────────────

    // 1 — GetState with no prior events → Initial
    [Fact]
    public async Task GetState_NoEvents_ReturnsInitial()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var state = await service.GetState(TalkId, "fr");

        state.State.Should().Be(TranslationWorkflowState.Initial);
        state.TalkId.Should().Be(TalkId);
        state.LanguageCode.Should().Be("fr");
        state.LastEventType.Should().BeNull();
        state.LastEventAt.Should().BeNull();
    }

    // 2 — GetHistory with no prior events → empty list
    [Fact]
    public async Task GetHistory_NoEvents_ReturnsEmpty()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var history = await service.GetHistory(TalkId, "de");

        history.Should().BeEmpty();
    }

    // 3 — StartTranslation from Initial (legal source state) → writes event
    [Fact]
    public async Task StartTranslation_WritesEvent()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "es", confirmOverwrite: false);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "es")
            .ToListAsync();

        events.Should().ContainSingle();
        events[0].EventType.Should().Be(WorkflowEventTypes.TranslationStarted);
        events[0].TriggeredByType.Should().Be(TriggeredByType.User);
    }

    // 10 — MarkStale from non-Stale state → writes MarkedStale event
    [Fact]
    public async Task MarkStale_WritesEvent()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.MarkStale(TalkId, "no");

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "no")
            .ToListAsync();
        events.Should().ContainSingle();
        events[0].EventType.Should().Be(WorkflowEventTypes.MarkedStale);
        events[0].TriggeredByType.Should().Be(TriggeredByType.User);
    }

    // ── 4-9 — updated: assert new failure behaviour ──────────────────────────
    // Previously these tests called service methods from illegal source states
    // (state = Initial in all cases) that were no-ops before Phase 3a guards.
    // They now assert the expected WorkflowInvalidState failure.
    //
    // Tests 7 & 8 are compound flows: their first step (InitiateExternalReview)
    // now fails from Initial. Rather than asserting a first-step failure that
    // duplicates test 20 (InitiateExternalReview_FromInitial), they have been
    // updated with proper state setup so the full compound flow succeeds —
    // preserving end-to-end coverage that would otherwise be lost.

    // 4 — StartValidation from Initial → WorkflowInvalidState  [was: StartValidation_WritesEvent]
    [Fact]
    public async Task StartValidation_FromInitial_ReturnsInvalidState()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartValidation(TalkId, "it");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInvalidState);
        result.Errors.Should().ContainSingle(e => e.Contains("Initial") && e.Contains("AIGenerated"));
    }

    // 5 — SubmitInternalReview from Initial → WorkflowInvalidState  [was: SubmitInternalReview_WritesEventAndReview]
    [Fact]
    public async Task SubmitInternalReview_FromInitial_ReturnsInvalidState()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.SubmitInternalReview(TalkId, "pt", accepted: true, editedContent: "edited");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInvalidState);
        result.Errors.Should().ContainSingle(e => e.Contains("Initial") && e.Contains("Validated"));
    }

    // 6 — InitiateExternalReview from Initial → WorkflowInvalidState  [was: InitiateExternalReview_WritesEventAndInvitation]
    [Fact]
    public async Task InitiateExternalReview_FromInitial_ReturnsInvalidState()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(TalkId, "nl", "reviewer@example.com");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInvalidState);
        result.Errors.Should().ContainSingle(e => e.Contains("Initial") && e.Contains("ReviewerAccepted"));
    }

    // 7 — SubmitExternalReview full token round-trip with proper state setup
    //     Updated: seeds ReviewerAccepted state first so InitiateExternalReview is legal,
    //     then verifies the complete external review submission flow end-to-end.
    [Fact]
    public async Task SubmitExternalReview_WithValidToken_WritesEventAndReview()
    {
        await SeedEventAsync(TalkId, "pl", WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        // Step 1: create invitation from ReviewerAccepted state
        var initiateResult = await service.InitiateExternalReview(TalkId, "pl", "reviewer@example.com");
        initiateResult.Success.Should().BeTrue();
        var rawToken = initiateResult.Data!.Token;
        var invitationId = initiateResult.Data.InvitationId;

        // Step 2: external reviewer submits via token (state is now AwaitingThirdParty)
        var submitResult = await service.SubmitExternalReview(rawToken, accepted: true, editedContent: "external edit");
        submitResult.Success.Should().BeTrue();

        var db = GetDbContext();

        var review = await db.Set<WorkflowReview>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.ExternalParticipantInvitationId == invitationId);
        review.Should().NotBeNull();
        review!.ReviewerType.Should().Be(ReviewerType.External);
        review.EditedContent.Should().Be("external edit");
        review.Accepted.Should().BeTrue();

        var invitation = await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .FirstAsync(i => i.Id == invitationId);
        invitation.Status.Should().Be(InvitationStatus.Used);
        invitation.UsedAt.Should().NotBeNull();

        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "pl")
            .ToListAsync();
        events.Should().Contain(e => e.EventType == WorkflowEventTypes.ExternalReviewInitiated);
        events.Should().Contain(e => e.EventType == WorkflowEventTypes.ExternalReviewSubmitted);
    }

    // 8 — ConfirmExternalReview with proper state setup
    //     Updated: seeds ReviewerAccepted state so the full initiate→submit→confirm chain is legal.
    [Fact]
    public async Task ConfirmExternalReview_WritesEvent()
    {
        await SeedEventAsync(TalkId, "sv", WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var initiateResult = await service.InitiateExternalReview(TalkId, "sv", "reviewer@example.com");
        await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: null);

        var result = await service.ConfirmExternalReview(TalkId, "sv", accepted: true);
        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "sv")
            .ToListAsync();
        events.Should().Contain(e => e.EventType == WorkflowEventTypes.ExternalReviewConfirmed);
    }

    // 9 — AcceptAsFinal from Initial → WorkflowInvalidState  [was: AcceptAsFinal_WritesEvent]
    [Fact]
    public async Task AcceptAsFinal_FromInitial_ReturnsInvalidState()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.AcceptAsFinal(TalkId, "da");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInvalidState);
        result.Errors.Should().ContainSingle(e => e.Contains("Initial") && e.Contains("Validated"));
    }

    // ── New guard tests ───────────────────────────────────────────────────────

    // 11 — StartTranslation from AIGenerated (legal) → success
    [Fact]
    public async Task StartTranslation_FromAIGenerated_Succeeds()
    {
        await SeedEventAsync(TalkId, "ar", WorkflowEventTypes.TranslationCompleted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "ar");

        result.Success.Should().BeTrue();
    }

    // 12 — StartTranslation from Stale (legal) → success
    [Fact]
    public async Task StartTranslation_FromStale_Succeeds()
    {
        await SeedEventAsync(TalkId, "zh", WorkflowEventTypes.MarkedStale);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "zh");

        result.Success.Should().BeTrue();
    }

    // 13 — StartTranslation from Validated (legal) → success
    [Fact]
    public async Task StartTranslation_FromValidated_Succeeds()
    {
        await SeedEventAsync(TalkId, "fi", WorkflowEventTypes.ValidationCompleted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "fi");

        result.Success.Should().BeTrue();
    }

    // 14 — StartTranslation from AwaitingThirdParty → WorkflowInvalidState (external review in progress)
    [Fact]
    public async Task StartTranslation_FromAwaitingThirdParty_ReturnsInvalidState()
    {
        await SeedEventAsync(TalkId, "ja", WorkflowEventTypes.ExternalReviewInitiated);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "ja");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInvalidState);
        result.Errors.Should().ContainSingle(e => e.Contains("AwaitingThirdParty"));
    }

    // 15 — StartTranslation from Accepted without confirmOverwrite → WorkflowConfirmationRequired
    [Fact]
    public async Task StartTranslation_FromAccepted_WithoutConfirm_ReturnsConfirmationRequired()
    {
        await SeedEventAsync(TalkId, "ko", WorkflowEventTypes.AcceptedAsFinal);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "ko", confirmOverwrite: false);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowConfirmationRequired);
        result.Errors.Should().ContainSingle(e => e.Contains("Accepted") && e.Contains("confirmOverwrite"));
    }

    // 16 — StartTranslation from Accepted with confirmOverwrite=true → success
    [Fact]
    public async Task StartTranslation_FromAccepted_WithConfirm_Succeeds()
    {
        await SeedEventAsync(TalkId, "ru", WorkflowEventTypes.AcceptedAsFinal);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "ru", confirmOverwrite: true);

        result.Success.Should().BeTrue();
    }

    // 17 — StartTranslation from ReviewerAccepted without confirmOverwrite → WorkflowConfirmationRequired
    [Fact]
    public async Task StartTranslation_FromReviewerAccepted_WithoutConfirm_ReturnsConfirmationRequired()
    {
        await SeedEventAsync(TalkId, "tr", WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "tr", confirmOverwrite: false);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowConfirmationRequired);
        result.Errors.Should().ContainSingle(e => e.Contains("ReviewerAccepted") && e.Contains("confirmOverwrite"));
    }

    // 18 — StartValidation from AIGenerated (legal) → success
    [Fact]
    public async Task StartValidation_FromAIGenerated_Succeeds()
    {
        await SeedEventAsync(TalkId, "cs", WorkflowEventTypes.TranslationCompleted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartValidation(TalkId, "cs");

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "cs"
                     && e.EventType == WorkflowEventTypes.ValidationStarted)
            .ToListAsync();
        events.Should().ContainSingle();
    }

    // 19 — SubmitInternalReview from Validated (legal) → writes event and review row
    [Fact]
    public async Task SubmitInternalReview_FromValidated_WritesEventAndReview()
    {
        await SeedEventAsync(TalkId, "hu", WorkflowEventTypes.ValidationCompleted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.SubmitInternalReview(TalkId, "hu", accepted: true, editedContent: "corrected");

        result.Success.Should().BeTrue();

        var db = GetDbContext();

        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "hu"
                     && e.EventType == WorkflowEventTypes.InternalReviewSubmitted)
            .ToListAsync();
        events.Should().ContainSingle();

        var reviews = await db.Set<WorkflowReview>()
            .IgnoreQueryFilters()
            .Where(r => r.WorkflowType == WorkflowType.Translation
                     && r.TargetEntityId == TalkId
                     && r.TargetEntitySubKey == "hu")
            .ToListAsync();
        reviews.Should().ContainSingle();
        reviews[0].ReviewerType.Should().Be(ReviewerType.Internal);
        reviews[0].EditedContent.Should().Be("corrected");
        reviews[0].Accepted.Should().BeTrue();
    }

    // 20 — InitiateExternalReview from ReviewerAccepted (legal) → writes event and invitation
    //      No setting seeded → defaults to 30-day lifetime.
    //      Verifies ContextType and ContextPayload placeholder populated (Phase 4.2a).
    [Fact]
    public async Task InitiateExternalReview_FromReviewerAccepted_WritesEventAndInvitation()
    {
        await SeedEventAsync(TalkId, "ro", WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(TalkId, "ro", "external@example.com");

        result.Success.Should().BeTrue();
        result.Data!.Token.Should().NotBeNullOrEmpty();
        // Default path: no ExternalParticipantTokenLifetimeDays setting seeded → 30 days
        result.Data.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(1));

        var db = GetDbContext();

        var invitation = await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == result.Data.InvitationId);
        invitation.Should().NotBeNull();
        invitation!.Status.Should().Be(InvitationStatus.Pending);
        invitation.InvitedEmail.Should().Be("external@example.com");
        invitation.TokenHash.Should().NotBe(result.Data.Token); // hash ≠ raw token
        invitation.ContextType.Should().Be("TranslationReview");
        invitation.ContextPayload.Should().Be("{\"contextType\":\"TranslationReview\"}");

        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "ro"
                     && e.EventType == WorkflowEventTypes.ExternalReviewInitiated)
            .ToListAsync();
        events.Should().ContainSingle();
    }

    // 21 — SubmitExternalReview with valid token but state not AwaitingThirdParty → WorkflowInvalidState
    //      Seeds an invitation directly (bypassing the service guard) so the token is valid,
    //      but the workflow state is Validated — not AwaitingThirdParty.
    [Fact]
    public async Task SubmitExternalReview_WithValidTokenButNotAwaitingThirdParty_ReturnsInvalidState()
    {
        await SeedEventAsync(TalkId, "hr", WorkflowEventTypes.ValidationCompleted); // state = Validated
        var rawToken = await SeedInvitationAsync(TalkId, "hr");

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.SubmitExternalReview(rawToken, accepted: true, editedContent: null);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInvalidState);
        result.Errors.Should().ContainSingle(e => e.Contains("Validated") && e.Contains("stale"));
    }

    // 22 — ConfirmExternalReview from Initial → WorkflowInvalidState
    [Fact]
    public async Task ConfirmExternalReview_FromInitial_ReturnsInvalidState()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.ConfirmExternalReview(TalkId, "sk", accepted: true);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInvalidState);
        result.Errors.Should().ContainSingle(e => e.Contains("Initial") && e.Contains("ThirdPartyReviewed"));
    }

    // 23 — AcceptAsFinal from Validated (legal) → success
    [Fact]
    public async Task AcceptAsFinal_FromValidated_Succeeds()
    {
        await SeedEventAsync(TalkId, "sl", WorkflowEventTypes.ValidationCompleted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.AcceptAsFinal(TalkId, "sl");

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "sl"
                     && e.EventType == WorkflowEventTypes.AcceptedAsFinal)
            .ToListAsync();
        events.Should().ContainSingle();
    }

    // 24 — AcceptAsFinal from ReviewerAccepted (legal) → success
    [Fact]
    public async Task AcceptAsFinal_FromReviewerAccepted_Succeeds()
    {
        await SeedEventAsync(TalkId, "bg", WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.AcceptAsFinal(TalkId, "bg");

        result.Success.Should().BeTrue();
    }

    // 25 — AcceptAsFinal from ThirdPartyReviewed (legal) → success
    [Fact]
    public async Task AcceptAsFinal_FromThirdPartyReviewed_Succeeds()
    {
        await SeedEventAsync(TalkId, "uk", WorkflowEventTypes.ExternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.AcceptAsFinal(TalkId, "uk");

        result.Success.Should().BeTrue();
    }

    // 26 — MarkStale from Stale (already stale) → success with no new event written
    [Fact]
    public async Task MarkStale_FromStale_ReturnsSuccessWithNoNewEvent()
    {
        await SeedEventAsync(TalkId, "el", WorkflowEventTypes.MarkedStale);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.MarkStale(TalkId, "el");

        result.Success.Should().BeTrue();

        // Must not have written a second MarkedStale row
        var db = GetDbContext();
        var count = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .CountAsync(e => e.WorkflowType == WorkflowType.Translation
                          && e.TargetEntityId == TalkId
                          && e.TargetEntitySubKey == "el");
        count.Should().Be(1, "MarkStale from Stale must be a no-op — no new event row");
    }

    // ── RecordTranslationCompleted tests ─────────────────────────────────────

    // 27 — RecordTranslationCompleted from TranslationStarted (legal in-flight state) → success, event written
    [Fact]
    public async Task RecordTranslationCompleted_FromTranslationStarted_Succeeds()
    {
        await SeedEventAsync(TalkId, "lb", WorkflowEventTypes.TranslationStarted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.RecordTranslationCompleted(TalkId, "lb");

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "lb")
            .OrderBy(e => e.OccurredAt)
            .ToListAsync();
        events.Should().HaveCount(2);
        events[0].EventType.Should().Be(WorkflowEventTypes.TranslationStarted);
        events[1].EventType.Should().Be(WorkflowEventTypes.TranslationCompleted);
    }

    // 28 — RecordTranslationCompleted from AIGenerated (already completed) → idempotent success, no new event
    [Fact]
    public async Task RecordTranslationCompleted_FromAIGenerated_ReturnsSuccessWithNoNewEvent()
    {
        await SeedEventAsync(TalkId, "mk", WorkflowEventTypes.TranslationCompleted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.RecordTranslationCompleted(TalkId, "mk");

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var count = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .CountAsync(e => e.WorkflowType == WorkflowType.Translation
                          && e.TargetEntityId == TalkId
                          && e.TargetEntitySubKey == "mk");
        count.Should().Be(1, "RecordTranslationCompleted from AIGenerated must be a no-op — no new event row");
    }

    // 29 — RecordTranslationCompleted from Accepted (illegal state) → WorkflowInvalidState
    [Fact]
    public async Task RecordTranslationCompleted_FromAccepted_ReturnsWorkflowInvalidState()
    {
        await SeedEventAsync(TalkId, "is", WorkflowEventTypes.AcceptedAsFinal);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.RecordTranslationCompleted(TalkId, "is");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInvalidState);
        result.Errors.Should().ContainSingle(e => e.Contains("Accepted") && e.Contains("Translating"));
    }

    // ── Translating state tests ───────────────────────────────────────────────

    // 30 — GetState after TranslationStarted only → Translating
    [Fact]
    public async Task GetState_AfterTranslationStartedOnly_ReturnsTranslating()
    {
        await SeedEventAsync(TalkId, "lv", WorkflowEventTypes.TranslationStarted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var state = await service.GetState(TalkId, "lv");

        state.State.Should().Be(TranslationWorkflowState.Translating);
    }

    // 31 — StartTranslation from Translating without confirmOverwrite → WorkflowConfirmationRequired
    [Fact]
    public async Task StartTranslation_FromTranslating_WithoutConfirmOverwrite_ReturnsConfirmationRequired()
    {
        await SeedEventAsync(TalkId, "lt", WorkflowEventTypes.TranslationStarted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "lt", confirmOverwrite: false);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowConfirmationRequired);
        result.Errors.Should().ContainSingle(e => e.Contains("in flight") && e.Contains("confirmOverwrite"));
    }

    // 32 — StartTranslation from Translating with confirmOverwrite=true → success, second TranslationStarted written
    [Fact]
    public async Task StartTranslation_FromTranslating_WithConfirmOverwrite_ReturnsSuccess()
    {
        await SeedEventAsync(TalkId, "mt", WorkflowEventTypes.TranslationStarted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "mt", confirmOverwrite: true);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "mt"
                     && e.EventType == WorkflowEventTypes.TranslationStarted)
            .ToListAsync();
        events.Should().HaveCount(2, "confirmOverwrite=true on an in-flight translation writes a second TranslationStarted event");
    }

    // ── TriggeredByType tests ─────────────────────────────────────────────────

    // 33 — StartTranslation with triggeredBy=System → event records System trigger with null user ID
    [Fact]
    public async Task StartTranslation_WithSystemTrigger_RecordsSystemAudit()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "kk", triggeredBy: TriggeredByType.System);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "kk")
            .ToListAsync();

        events.Should().ContainSingle();
        events[0].EventType.Should().Be(WorkflowEventTypes.TranslationStarted);
        events[0].TriggeredByType.Should().Be(TriggeredByType.System);
        events[0].TriggeredByUserId.Should().BeNull("no user identity is available in a background job context");
    }

    // 34 — StartTranslation with default triggeredBy (User) → event records User trigger
    //      Locks in the default behaviour so a refactor cannot silently change it.
    [Fact]
    public async Task StartTranslation_WithUserTrigger_RecordsUserAudit()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        // No triggeredBy specified — defaults to TriggeredByType.User
        var result = await service.StartTranslation(TalkId, "uz");

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "uz")
            .ToListAsync();

        events.Should().ContainSingle();
        events[0].EventType.Should().Be(WorkflowEventTypes.TranslationStarted);
        events[0].TriggeredByType.Should().Be(TriggeredByType.User);
        // In a non-HTTP test scope ICurrentUserService.UserIdGuid = Guid.Empty → NullIfEmpty → null
        events[0].TriggeredByUserId.Should().BeNull();
    }
}
