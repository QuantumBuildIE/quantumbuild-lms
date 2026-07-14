using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
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
    private async Task<string> SeedInvitationAsync(
        Guid talkId, string languageCode, string invitedEmail = "test@example.com",
        List<int>? editableSectionIndices = null)
    {
        var rawToken = Guid.NewGuid().ToString("N");
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

        var db = GetDbContext();
        db.Set<ExternalParticipantInvitation>().Add(new ExternalParticipantInvitation
        {
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            InvitedEmail = invitedEmail,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Status = InvitationStatus.Pending,
            EditableSectionIndices = editableSectionIndices,
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

    // ── Chunk B — editableSectionIndices selection at initiation time ────────

    // 6a — null editableSectionIndices → full-scope review, persists null (pre-Chunk-B behaviour)
    [Fact]
    public async Task InitiateExternalReview_WithNoEditableIndices_UsesFullScope()
    {
        const string lang = "b1";
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = Guid.NewGuid(), Title = "Section 1", Content = "Content one" },
            new { SectionId = Guid.NewGuid(), Title = "Section 2", Content = "Content two" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var invitation = await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .FirstAsync(i => i.Id == result.Data!.InvitationId);

        invitation.EditableSectionIndicesJson.Should().BeNull();
        invitation.EditableSectionIndices.Should().BeNull();
    }

    // 6b — valid non-null list → persists and round-trips via the entity helper
    [Fact]
    public async Task InitiateExternalReview_WithValidEditableIndices_PersistsList()
    {
        const string lang = "b2";
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = Guid.NewGuid(), Title = "Section 1", Content = "Content one" },
            new { SectionId = Guid.NewGuid(), Title = "Section 2", Content = "Content two" },
            new { SectionId = Guid.NewGuid(), Title = "Section 3", Content = "Content three" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(
            TalkId, lang, "reviewer@example.com", new List<int> { 0, 2 });

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var invitation = await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .FirstAsync(i => i.Id == result.Data!.InvitationId);

        invitation.EditableSectionIndices.Should().BeEquivalentTo(new List<int> { 0, 2 });
    }

    // 6c — empty list → rejected (at least one section must be selected)
    [Fact]
    public async Task InitiateExternalReview_WithEmptyList_ReturnsFailure()
    {
        const string lang = "b3";
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = Guid.NewGuid(), Title = "Section 1", Content = "Content one" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(
            TalkId, lang, "reviewer@example.com", new List<int>());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInitiationInvalid);
    }

    // 6d — index >= section count → rejected
    [Fact]
    public async Task InitiateExternalReview_WithOutOfRangeIndex_ReturnsFailure()
    {
        const string lang = "b4";
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = Guid.NewGuid(), Title = "Section 1", Content = "Content one" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(
            TalkId, lang, "reviewer@example.com", new List<int> { 5 });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInitiationInvalid);
    }

    // 6e — duplicate indices → rejected
    [Fact]
    public async Task InitiateExternalReview_WithDuplicateIndices_ReturnsFailure()
    {
        const string lang = "b5";
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = Guid.NewGuid(), Title = "Section 1", Content = "Content one" },
            new { SectionId = Guid.NewGuid(), Title = "Section 2", Content = "Content two" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(
            TalkId, lang, "reviewer@example.com", new List<int> { 0, 0 });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInitiationInvalid);
    }

    // 7 — SubmitExternalReview full token round-trip with proper state setup
    //     Updated: seeds ReviewerAccepted state first so InitiateExternalReview is legal,
    //     then verifies the complete external review submission flow end-to-end.
    [Fact]
    public async Task SubmitExternalReview_WithValidToken_WritesEventAndReview()
    {
        var sectionId = Guid.NewGuid();
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = sectionId, Title = "Section Title", Content = "Original AI translation" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, "pl", originalSectionsJson);
        await SeedEventAsync(TalkId, "pl", WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        // Step 1: create invitation from ReviewerAccepted state
        var initiateResult = await service.InitiateExternalReview(TalkId, "pl", "reviewer@example.com");
        initiateResult.Success.Should().BeTrue();
        var rawToken = initiateResult.Data!.Token;
        var invitationId = initiateResult.Data.InvitationId;

        // Step 2: external reviewer submits via token (state is now AwaitingThirdParty).
        // editedContent must be a valid [{sectionIndex, translatedText}] JSON array (camelCase —
        // matches ExternalReviewEditedSectionDto's JsonPropertyName attributes and the frontend
        // wire contract) — auto-apply gates reject free-text content on the accept path (see
        // validation gate tests).
        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 0, translatedText = "Reviewer edited translation" }
        });
        var submitResult = await service.SubmitExternalReview(rawToken, accepted: true, editedContent: editsJson);
        submitResult.Success.Should().BeTrue();

        var db = GetDbContext();

        var review = await db.Set<WorkflowReview>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.ExternalParticipantInvitationId == invitationId);
        review.Should().NotBeNull();
        review!.ReviewerType.Should().Be(ReviewerType.External);
        review.EditedContent.Should().Be(editsJson);
        review.Accepted.Should().BeTrue();

        var invitation = await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .FirstAsync(i => i.Id == invitationId);
        invitation.Status.Should().Be(InvitationStatus.Used);
        invitation.UsedAt.Should().NotBeNull();

        // Auto-apply: the reviewer's edit is already live in TranslatedSections and provenance
        // is stamped — no separate admin confirmation step exists anymore.
        var translation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == "pl" && !t.IsDeleted);
        var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation.TranslatedSections);
        sections![0].Content.Should().Be("Reviewer edited translation");
        translation.LastExternalReviewedAt.Should().NotBeNull();
        translation.LastExternalReviewedBy.Should().Be("reviewer@example.com");

        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "pl")
            .ToListAsync();
        events.Should().Contain(e => e.EventType == WorkflowEventTypes.ExternalReviewInitiated);
        events.Should().Contain(e => e.EventType == WorkflowEventTypes.ExternalReviewSubmitted);
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
        // No validation run seeded for this talk/language → flaggedWordCount = 0
        invitation.ContextPayload.Should().Be("{\"contextType\":\"TranslationReview\",\"flaggedWordCount\":0}");

        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "ro"
                     && e.EventType == WorkflowEventTypes.ExternalReviewInitiated)
            .ToListAsync();
        events.Should().ContainSingle();
    }

    // ── Chunk F — second-round external review from ThirdPartyReviewed ───────

    // F1 — InitiateExternalReview from ThirdPartyReviewed (legal, Chunk F) → writes a new event
    //      and a fresh invitation, transitions state back to AwaitingThirdParty.
    [Fact]
    public async Task InitiateExternalReview_FromThirdPartyReviewed_TransitionsToAwaitingThirdParty()
    {
        const string lang = "f1";
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = Guid.NewGuid(), Title = "Section 1", Content = "Content one" },
            new { SectionId = Guid.NewGuid(), Title = "Section 2", Content = "Content two" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.ExternalReviewSubmitted); // state = ThirdPartyReviewed

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(
            TalkId, lang, "second-round@example.com", new List<int> { 1 });

        result.Success.Should().BeTrue();
        result.Data!.Token.Should().NotBeNullOrEmpty();

        var stateAfter = await service.GetState(TalkId, lang);
        stateAfter.State.Should().Be(TranslationWorkflowState.AwaitingThirdParty);

        var db = GetDbContext();
        var invitation = await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == result.Data.InvitationId);
        invitation.Should().NotBeNull();
        invitation!.Status.Should().Be(InvitationStatus.Pending);
        invitation.InvitedEmail.Should().Be("second-round@example.com");
        invitation.EditableSectionIndices.Should().BeEquivalentTo(new List<int> { 1 });

        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == lang
                     && e.EventType == WorkflowEventTypes.ExternalReviewInitiated)
            .ToListAsync();
        events.Should().ContainSingle();
    }

    // F2 — InitiateExternalReview from Accepted (terminal, Chunk F) → rejected, WorkflowInvalidState.
    //      Accepted stays terminal by design — admin must delete-and-recreate the talk to reset.
    [Fact]
    public async Task InitiateExternalReview_FromAccepted_ReturnsFailure()
    {
        const string lang = "f2";
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.AcceptedAsFinal);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInvalidState);
        result.Errors.Should().ContainSingle(e => e.Contains("Accepted"));
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

    // ── Phase 4.2b — FlaggedWordCount in ContextPayload ──────────────────────

    // 35 — InitiateExternalReview seeds a completed validation run with flags and asserts
    //      the ContextPayload contains the computed FlaggedWordCount.
    //
    //  Result 1: OriginalText = "The quick brown fox jumps over the lazy dog" (9 words)
    //    Flag A: [4..9)  → "quick"      → 1 word
    //    Flag B: [16..19) → "fox"       → 1 word
    //    Merged (non-overlapping): (4,9) and (16,19) → 1 + 1 = 2 words
    //  Result 2: OriginalText = "Hello world" (2 words)
    //    Flag C: [0..11) → "Hello world" → 2 words
    //  Expected total: 1 + 1 + 2 = 4
    [Fact]
    public async Task InitiateExternalReview_PopulatesContextPayloadWithFlaggedWordCount()
    {
        const string lang = "xh";

        // Pre-condition: ReviewerAccepted state
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        // Seed a completed validation run for (TalkId, lang)
        var runId = await SeedValidationRunAsync(TalkId, lang);

        // Seed Result 1 with two non-overlapping flags
        var result1Id = await SeedValidationResultAsync(runId, sectionIndex: 0,
            originalText: "The quick brown fox jumps over the lazy dog");
        await SeedFlagAsync(result1Id, startOffset: 4, endOffset: 9);   // "quick" → 1 word
        await SeedFlagAsync(result1Id, startOffset: 16, endOffset: 19); // "fox"   → 1 word

        // Seed Result 2 with a flag spanning the whole text
        var result2Id = await SeedValidationResultAsync(runId, sectionIndex: 1,
            originalText: "Hello world");
        await SeedFlagAsync(result2Id, startOffset: 0, endOffset: 11);  // "Hello world" → 2 words

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var invitation = await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == result.Data!.InvitationId);

        invitation.Should().NotBeNull();
        invitation!.ContextType.Should().Be("TranslationReview");

        using var doc = JsonDocument.Parse(invitation.ContextPayload!);
        doc.RootElement.GetProperty("contextType").GetString().Should().Be("TranslationReview");
        doc.RootElement.GetProperty("flaggedWordCount").GetInt32().Should().Be(4);
    }

    // ── Phase 4.4 — invitation email dispatch ────────────────────────────────

    // 36 — InitiateExternalReview on success triggers exactly one invitation email with the correct params
    [Fact]
    public async Task InitiateExternalReview_OnSuccess_TriggersEmailSend()
    {
        const string lang = "et";
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        FakeEmailService.Reset();

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

        result.Success.Should().BeTrue();

        FakeEmailService.SentInvitations.Should().HaveCount(1);

        var sent = FakeEmailService.SentInvitations[0];
        sent.ReviewerEmail.Should().Be("reviewer@example.com");
        sent.PortalUrl.Should().Contain(result.Data!.Token);
        sent.ExpiresAt.Should().BeCloseTo(result.Data.ExpiresAt, TimeSpan.FromSeconds(5));
    }

    // 37 — InitiateExternalReview when email delivery throws still returns Ok and persists the invitation
    [Fact]
    public async Task InitiateExternalReview_WhenEmailFails_StillReturnsOkAndCreatesInvitation()
    {
        const string lang = "gl";
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        FakeEmailService.Reset();
        FakeEmailService.ShouldThrowOnInvitationEmail = true;

        try
        {
            using var scope = Factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

            var result = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

            result.Success.Should().BeTrue("email failure must not surface as a service error");

            var db = GetDbContext();
            var invitation = await db.Set<ExternalParticipantInvitation>()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == result.Data!.InvitationId);
            invitation.Should().NotBeNull("invitation row must be persisted regardless of email outcome");
            invitation!.Status.Should().Be(InvitationStatus.Pending);
        }
        finally
        {
            FakeEmailService.Reset();
        }
    }

    // ── Seed helpers for Phase 4.2b tests ────────────────────────────────────

    /// <summary>
    /// Seeds a completed TranslationValidationRun for (talkId, languageCode).
    /// TenantId is left unset so it is auto-stamped to Guid.Empty — consistent with
    /// how the service is resolved in the non-HTTP test scope.
    /// </summary>
    private async Task<Guid> SeedValidationRunAsync(Guid talkId, string languageCode)
    {
        var runId = Guid.NewGuid();
        var db = GetDbContext();
        db.Set<TranslationValidationRun>().Add(new TranslationValidationRun
        {
            Id = runId,
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            Status = ValidationRunStatus.Completed,
            OverallOutcome = ValidationOutcome.Pass,
            PassThreshold = 75,
            SourceLanguage = "en",
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return runId;
    }

    /// <summary>
    /// Seeds a TranslationValidationResult (BaseEntity — no TenantId) for the given run.
    /// </summary>
    private async Task<Guid> SeedValidationResultAsync(Guid runId, int sectionIndex, string originalText)
    {
        var resultId = Guid.NewGuid();
        var db = GetDbContext();
        db.Set<TranslationValidationResult>().Add(new TranslationValidationResult
        {
            Id = resultId,
            ValidationRunId = runId,
            SectionIndex = sectionIndex,
            SectionTitle = $"Section {sectionIndex}",
            OriginalText = originalText,
            TranslatedText = string.Empty,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
        return resultId;
    }

    /// <summary>
    /// Seeds a TranslationFlag (TenantEntity) linked to the given result.
    /// TenantId is left unset so it is auto-stamped to Guid.Empty, matching the
    /// non-HTTP test scope's tenant filter predicate.
    /// </summary>
    private async Task SeedFlagAsync(Guid resultId, int startOffset, int endOffset)
    {
        var db = GetDbContext();
        db.Set<TranslationFlag>().Add(new TranslationFlag
        {
            ToolboxTalkId = TalkId,
            LanguageCode = "xh",
            ValidationResultId = resultId,
            StartOffset = startOffset,
            EndOffset = endOffset,
            Severity = FlagSeverity.Warning,
            Reason = "test flag",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds a TranslationFlag (TenantEntity) linked to the given result for a specific language code.
    /// TenantId is left unset so it is auto-stamped to Guid.Empty, matching the
    /// non-HTTP test scope's tenant filter predicate.
    /// </summary>
    private async Task SeedFlagForLangAsync(Guid resultId, int startOffset, int endOffset, string languageCode)
    {
        var db = GetDbContext();
        db.Set<TranslationFlag>().Add(new TranslationFlag
        {
            ToolboxTalkId = TalkId,
            LanguageCode = languageCode,
            ValidationResultId = resultId,
            StartOffset = startOffset,
            EndOffset = endOffset,
            Severity = FlagSeverity.Warning,
            Reason = "test flag",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();
    }

    // ── Seed helpers for Phase 4.5a tests ────────────────────────────────────

    /// <summary>
    /// Seeds a ToolboxTalkTranslation for (talkId, languageCode) with the given JSON sections.
    /// TenantId is auto-stamped to Guid.Empty in the non-HTTP test scope.
    /// </summary>
    private async Task SeedToolboxTalkTranslationAsync(
        Guid talkId, string languageCode, string translatedSectionsJson)
    {
        var db = GetDbContext();
        db.Set<ToolboxTalkTranslation>().Add(new ToolboxTalkTranslation
        {
            ToolboxTalkId = talkId,
            LanguageCode = languageCode,
            TranslatedTitle = $"Test translation ({languageCode})",
            TranslatedSections = translatedSectionsJson,
            TranslatedAt = DateTime.UtcNow,
            TranslationProvider = "test"
        });
        await db.SaveChangesAsync();
    }

    // ── Phase 4.5a → auto-apply — SubmitExternalReview merges reviewer edits directly ─
    // ConfirmExternalReview no longer exists (docs/external-review-auto-apply-recon.md, Option A):
    // auto-apply happens inside SubmitExternalReview itself when accepted=true, gated by
    // ValidateExternalReviewSubmissionAsync's four validation gates.

    // 38 — SubmitExternalReview with accepted=true propagates all edits into TranslatedSections,
    //      preserving SectionId and Title on each section, and stamps provenance fields.
    [Fact]
    public async Task SubmitExternalReview_AcceptedTrue_AutoAppliesAllEditsAndSetsProvenance()
    {
        const string lang = "xk";
        var section1Id = Guid.NewGuid();
        var section2Id = Guid.NewGuid();

        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = section1Id, Title = "Section 1 Title", Content = "Original section one content" },
            new { SectionId = section2Id, Title = "Section 2 Title", Content = "Original section two content" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);

        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var initiateResult = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");
        initiateResult.Success.Should().BeTrue();

        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 0, translatedText = "Edited section one" },
            new { sectionIndex = 1, translatedText = "Edited section two" }
        });
        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: editsJson);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var translation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == lang && !t.IsDeleted);

        translation.Should().NotBeNull();
        var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation!.TranslatedSections);
        sections.Should().HaveCount(2);
        sections![0].Content.Should().Be("Edited section one");
        sections[1].Content.Should().Be("Edited section two");
        sections[0].SectionId.Should().Be(section1Id, "SectionId must be preserved");
        sections[0].Title.Should().Be("Section 1 Title", "Title must be preserved");
        sections[1].SectionId.Should().Be(section2Id, "SectionId must be preserved");
        sections[1].Title.Should().Be("Section 2 Title", "Title must be preserved");
        translation.LastExternalReviewedAt.Should().NotBeNull("auto-apply must stamp provenance");
        translation.LastExternalReviewedBy.Should().Be("reviewer@example.com", "provenance is sourced from the invitation's InvitedEmail");

        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == lang
                     && e.EventType == WorkflowEventTypes.ExternalReviewSubmitted)
            .ToListAsync();
        events.Should().ContainSingle();
    }

    // 39 — SubmitExternalReview with accepted=false does NOT propagate edits or stamp provenance;
    //      TranslatedSections retains original content. No validation gates run on this path.
    [Fact]
    public async Task SubmitExternalReview_AcceptedFalse_DoesNotPropagateEditsOrStampProvenance()
    {
        const string lang = "so";
        var section1Id = Guid.NewGuid();
        var section2Id = Guid.NewGuid();

        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = section1Id, Title = "Section 1 Title", Content = "Original section one content" },
            new { SectionId = section2Id, Title = "Section 2 Title", Content = "Original section two content" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);

        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var initiateResult = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

        // Reviewer rejects via the same submit form — not the free-text decline endpoint —
        // so editedContent is not necessarily well-formed edit JSON.
        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: false, editedContent: "Not satisfied with this translation");

        result.Success.Should().BeTrue("gates only run on the accept path");

        var db = GetDbContext();
        var translation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == lang && !t.IsDeleted);

        translation.Should().NotBeNull();
        var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation!.TranslatedSections);
        sections![0].Content.Should().Be("Original section one content", "rejected submissions must not be propagated");
        sections[1].Content.Should().Be("Original section two content", "rejected submissions must not be propagated");
        translation.LastExternalReviewedAt.Should().BeNull("nothing was applied, so provenance must not be stamped");
        translation.LastExternalReviewedBy.Should().BeNull();

        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == lang
                     && e.EventType == WorkflowEventTypes.ExternalReviewSubmitted)
            .ToListAsync();
        events.Should().ContainSingle();
    }

    // 40 — SubmitExternalReview with accepted=true and edits for only one of three sections
    //      updates only the edited section; others are untouched.
    [Fact]
    public async Task SubmitExternalReview_AcceptedTrue_PartialEdits_UpdatesOnlyEditedSections()
    {
        const string lang = "sq";
        var section1Id = Guid.NewGuid();
        var section2Id = Guid.NewGuid();
        var section3Id = Guid.NewGuid();

        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = section1Id, Title = "Section 1", Content = "Original content one" },
            new { SectionId = section2Id, Title = "Section 2", Content = "Original content two" },
            new { SectionId = section3Id, Title = "Section 3", Content = "Original content three" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);

        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var initiateResult = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

        // Reviewer only edited section index 1 — the portal always submits every section, but the
        // gates only require the submitted entries to be well-formed, not exhaustive.
        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 1, translatedText = "Only section two was edited" }
        });
        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: editsJson);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var translation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == lang && !t.IsDeleted);

        var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation!.TranslatedSections);
        sections.Should().HaveCount(3);
        sections![0].Content.Should().Be("Original content one", "section 0 was not edited");
        sections[1].Content.Should().Be("Only section two was edited", "section 1 edit must be applied");
        sections[2].Content.Should().Be("Original content three", "section 2 was not edited");
    }

    // ── Chunk D — Gate 2 tightening: invitation-scoped editable indices ─────────

    // d-a — invitation scoped to a subset of sections: edits within the set are accepted,
    //       and only the edited sections receive Content + ReviewedAt/ReviewedBy provenance.
    [Fact]
    public async Task Submit_WithInvitationScopedToSubset_AcceptsInScopeIndices()
    {
        const string lang = "d1";
        var sectionIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var originalSectionsJson = JsonSerializer.Serialize(sectionIds.Select((id, i) =>
            new { SectionId = id, Title = $"Section {i}", Content = $"Original content {i}" }));
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var initiateResult = await service.InitiateExternalReview(
            TalkId, lang, "reviewer@example.com", new List<int> { 1, 3 });
        initiateResult.Success.Should().BeTrue();

        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 1, translatedText = "Edited section one" },
            new { sectionIndex = 3, translatedText = "Edited section three" }
        });
        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: editsJson);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var translation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == lang && !t.IsDeleted);
        var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation.TranslatedSections)!;

        sections[1].Content.Should().Be("Edited section one");
        sections[3].Content.Should().Be("Edited section three");
        sections[1].ReviewedAt.Should().NotBeNull();
        sections[1].ReviewedBy.Should().Be("reviewer@example.com");
        sections[3].ReviewedAt.Should().NotBeNull();
        sections[3].ReviewedBy.Should().Be("reviewer@example.com");

        foreach (var i in new[] { 0, 2, 4 })
        {
            sections[i].Content.Should().Be($"Original content {i}", "sections outside the submission are untouched");
            sections[i].ReviewedAt.Should().BeNull("sections not part of this submission must not gain provenance");
            sections[i].ReviewedBy.Should().BeNull();
        }
    }

    // d-b — invitation scoped to a subset of sections: an edit for an index outside the set is
    //       rejected before any write (Gate 2), even though the index is in range against the
    //       live translation.
    [Fact]
    public async Task Submit_WithInvitationScopedToSubset_RejectsOutOfScopeIndex()
    {
        const string lang = "d2";
        var sectionIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var originalSectionsJson = JsonSerializer.Serialize(sectionIds.Select((id, i) =>
            new { SectionId = id, Title = $"Section {i}", Content = $"Original content {i}" }));
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var initiateResult = await service.InitiateExternalReview(
            TalkId, lang, "reviewer@example.com", new List<int> { 1, 3 });
        initiateResult.Success.Should().BeTrue();

        // Section 2 is in range against the translation (5 sections) but not in the editable set.
        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 1, translatedText = "Edited section one" },
            new { sectionIndex = 2, translatedText = "Not selected for review" }
        });
        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: editsJson);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowSubmissionInvalid);

        await AssertNoWritesOccurred(TalkId, lang, "Original content 0");
    }

    // d-c — invitation with null EditableSectionIndices (pre-Chunk-B / explicit full-scope)
    //       behaves exactly as before: any in-range index is accepted.
    [Fact]
    public async Task Submit_WithInvitationNullEditableIndices_BehavesAsFullScope()
    {
        const string lang = "d3";
        var sectionIds = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var originalSectionsJson = JsonSerializer.Serialize(sectionIds.Select((id, i) =>
            new { SectionId = id, Title = $"Section {i}", Content = $"Original content {i}" }));
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        // No editableSectionIndices passed → invitation persists EditableSectionIndices = null.
        var initiateResult = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");
        initiateResult.Success.Should().BeTrue();

        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 0, translatedText = "Edited section zero" },
            new { sectionIndex = 2, translatedText = "Edited section two" }
        });
        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: editsJson);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var translation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == lang && !t.IsDeleted);
        var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation.TranslatedSections)!;

        sections[0].Content.Should().Be("Edited section zero");
        sections[0].ReviewedAt.Should().NotBeNull();
        sections[0].ReviewedBy.Should().Be("reviewer@example.com");
        sections[2].Content.Should().Be("Edited section two");
        sections[2].ReviewedAt.Should().NotBeNull();
        sections[2].ReviewedBy.Should().Be("reviewer@example.com");
    }

    // d-d — a section carrying provenance from an earlier review round is untouched by a later
    //       round that doesn't include it in its submission; only the sections in the later
    //       round's (differently-scoped) submission gain new provenance.
    //
    //       Chunk F: now drives a genuine second InitiateExternalReview/SubmitExternalReview
    //       round through the real service flow (the AwaitingThirdParty→ThirdPartyReviewed
    //       state guard that previously blocked this has been relaxed).
    [Fact]
    public async Task Submit_SecondRound_PreservesFirstRoundProvenance()
    {
        const string lang = "d4";
        var sectionIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var originalSectionsJson = JsonSerializer.Serialize(sectionIds.Select((id, i) =>
            new { SectionId = id, Title = $"Section {i}", Content = $"Original content {i}" }));
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        // Round one: scoped to section 0 only.
        const string firstReviewerEmail = "first-reviewer@example.com";
        var firstInitiate = await service.InitiateExternalReview(
            TalkId, lang, firstReviewerEmail, new List<int> { 0 });
        firstInitiate.Success.Should().BeTrue();

        var firstEditsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 0, translatedText = "First-round edited content" }
        });
        var firstSubmit = await service.SubmitExternalReview(
            firstInitiate.Data!.Token, accepted: true, editedContent: firstEditsJson);
        firstSubmit.Success.Should().BeTrue();

        var stateAfterFirstRound = await service.GetState(TalkId, lang);
        stateAfterFirstRound.State.Should().Be(TranslationWorkflowState.ThirdPartyReviewed);

        var firstRoundReviewedAt = JsonSerializer
            .Deserialize<List<TranslatedSectionSnapshot>>(
                (await GetDbContext().Set<ToolboxTalkTranslation>().IgnoreQueryFilters()
                    .FirstAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == lang && !t.IsDeleted))
                .TranslatedSections)![0].ReviewedAt!.Value;

        // Round two: real InitiateExternalReview from ThirdPartyReviewed, scoped to sections 2 and 4.
        const string secondReviewerEmail = "second-reviewer@example.com";
        var secondInitiate = await service.InitiateExternalReview(
            TalkId, lang, secondReviewerEmail, new List<int> { 2, 4 });
        secondInitiate.Success.Should().BeTrue();

        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 2, translatedText = "Second-round edited content" },
            new { sectionIndex = 4, translatedText = "Second-round edited content two" }
        });
        var result = await service.SubmitExternalReview(secondInitiate.Data!.Token, accepted: true, editedContent: editsJson);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var translation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == lang && !t.IsDeleted);
        var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation.TranslatedSections)!;

        sections[0].Content.Should().Be("First-round edited content", "section 0 was not part of round two's submission");
        sections[0].ReviewedAt.Should().Be(firstRoundReviewedAt, "round two must not touch round one's provenance");
        sections[0].ReviewedBy.Should().Be(firstReviewerEmail);

        sections[2].Content.Should().Be("Second-round edited content");
        sections[2].ReviewedBy.Should().Be(secondReviewerEmail);
        sections[4].Content.Should().Be("Second-round edited content two");
        sections[4].ReviewedBy.Should().Be(secondReviewerEmail);

        // Prior-invitation lifecycle: round one's invitation is marked Used on submit and its
        // token cannot be replayed to reopen a round; round two got its own fresh invitation row.
        var firstInvitation = await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .FirstAsync(i => i.Id == firstInitiate.Data.InvitationId);
        firstInvitation.Status.Should().Be(InvitationStatus.Used);

        var secondInvitationId = secondInitiate.Data!.InvitationId;
        secondInvitationId.Should().NotBe(firstInitiate.Data.InvitationId);

        var replayAttempt = await service.SubmitExternalReview(
            firstInitiate.Data.Token, accepted: true, editedContent: firstEditsJson);
        replayAttempt.Success.Should().BeFalse();
        replayAttempt.ErrorCode.Should().Be(FailureCode.WorkflowTokenAlreadyUsed);
    }

    // d-e / Chunk E — whole-translation LastExternalReviewedAt/By are a derived aggregate,
    //       computed at write time from the most recent ReviewedAt across ALL sections (not
    //       just the sections in this submission). A scoped submission still updates the
    //       aggregate here because the sections it touches (1, 3) are the only ones with any
    //       provenance at all, so they trivially hold the max.
    [Fact]
    public async Task Submit_WithScopedInvitation_DerivesWholeTranslationFlagsFromSections()
    {
        const string lang = "d5";
        var sectionIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var originalSectionsJson = JsonSerializer.Serialize(sectionIds.Select((id, i) =>
            new { SectionId = id, Title = $"Section {i}", Content = $"Original content {i}" }));
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var initiateResult = await service.InitiateExternalReview(
            TalkId, lang, "reviewer@example.com", new List<int> { 1, 3 });
        initiateResult.Success.Should().BeTrue();

        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 1, translatedText = "Edited section one" },
            new { sectionIndex = 3, translatedText = "Edited section three" }
        });
        var beforeSubmit = DateTime.UtcNow;
        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: editsJson);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var translation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == lang && !t.IsDeleted);
        var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation.TranslatedSections)!;

        var maxReviewedAtOnSections = sections
            .Where(s => s.ReviewedAt.HasValue)
            .Max(s => s.ReviewedAt!.Value);
        var sectionWithMaxReviewedAt = sections.First(s => s.ReviewedAt == maxReviewedAtOnSections);

        translation.LastExternalReviewedAt.Should().NotBeNull();
        translation.LastExternalReviewedAt!.Value.Should().BeOnOrAfter(beforeSubmit);
        translation.LastExternalReviewedAt.Should().Be(maxReviewedAtOnSections,
            "the whole-translation flag is derived from the max ReviewedAt across sections, not written independently");
        translation.LastExternalReviewedBy.Should().Be(sectionWithMaxReviewedAt.ReviewedBy);
        translation.LastExternalReviewedBy.Should().Be("reviewer@example.com");
    }

    // Chunk E — a scoped second round only touches sections outside round one's provenance
    // (sections 2 and 4), but because round two's ReviewedAt is later than round one's, the
    // derived whole-translation aggregate must reflect round two's reviewer/timestamp — not
    // round one's, even though round one's section (0) still carries its own untouched
    // provenance in the per-section list.
    [Fact]
    public async Task Submit_MultiRoundScoped_UsesMostRecentAsAggregate()
    {
        const string lang = "d6";
        var sectionIds = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var firstRoundReviewedAt = DateTime.UtcNow.AddDays(-3);
        const string firstReviewerEmail = "first-reviewer@example.com";

        var originalSectionsJson = JsonSerializer.Serialize(new object[]
        {
            new
            {
                SectionId = sectionIds[0], Title = "Section 0", Content = "First-round edited content",
                ReviewedAt = firstRoundReviewedAt, ReviewedBy = firstReviewerEmail
            },
            new { SectionId = sectionIds[1], Title = "Section 1", Content = "Original content 1" },
            new { SectionId = sectionIds[2], Title = "Section 2", Content = "Original content 2" },
            new { SectionId = sectionIds[3], Title = "Section 3", Content = "Original content 3" },
            new { SectionId = sectionIds[4], Title = "Section 4", Content = "Original content 4" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);

        // Seed the whole-translation columns as round one would have left them, so the test
        // proves round two's derivation overwrites a stale aggregate rather than happening to
        // already hold the right value.
        var db = GetDbContext();
        var seededTranslation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == lang && !t.IsDeleted);
        seededTranslation.LastExternalReviewedAt = firstRoundReviewedAt;
        seededTranslation.LastExternalReviewedBy = firstReviewerEmail;
        await db.SaveChangesAsync();

        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.ExternalReviewInitiated);
        const string secondReviewerEmail = "second-reviewer@example.com";
        var rawToken = await SeedInvitationAsync(
            TalkId, lang, secondReviewerEmail, new List<int> { 2, 4 });

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 2, translatedText = "Second-round edited content" },
            new { sectionIndex = 4, translatedText = "Second-round edited content two" }
        });
        var beforeSubmit = DateTime.UtcNow;
        var result = await service.SubmitExternalReview(rawToken, accepted: true, editedContent: editsJson);

        result.Success.Should().BeTrue();

        var translation = await GetDbContext().Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.ToolboxTalkId == TalkId && t.LanguageCode == lang && !t.IsDeleted);

        translation.LastExternalReviewedAt.Should().NotBeNull();
        translation.LastExternalReviewedAt!.Value.Should().BeOnOrAfter(beforeSubmit,
            "round two's ReviewedAt is more recent than round one's and must win the aggregate");
        translation.LastExternalReviewedBy.Should().Be(secondReviewerEmail);
    }

    // ── Validation gates ───────────────────────────────────────────────────────

    // Gate 1 — empty edited content on the accept path is rejected before any write.
    [Fact]
    public async Task SubmitExternalReview_EmptyEditedContent_Returns400WithSubmissionInvalid()
    {
        const string lang = "ha";
        var sectionId = Guid.NewGuid();
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = sectionId, Title = "Section Title", Content = "Original content" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();
        var initiateResult = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: null);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowSubmissionInvalid);

        await AssertNoWritesOccurred(TalkId, lang, "Original content");
    }

    // Gate 2 — a submitted SectionIndex outside the live translation's section count is rejected.
    [Fact]
    public async Task SubmitExternalReview_SectionIndexOutOfRange_Returns400WithSubmissionInvalid()
    {
        const string lang = "ig";
        var sectionId = Guid.NewGuid();
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = sectionId, Title = "Section Title", Content = "Original content" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();
        var initiateResult = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 5, translatedText = "This index does not exist" }
        });
        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: editsJson);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowSubmissionInvalid);

        await AssertNoWritesOccurred(TalkId, lang, "Original content");
    }

    // Gate 3 — a submitted section with blank text is rejected.
    [Fact]
    public async Task SubmitExternalReview_SectionWithBlankText_Returns400WithSubmissionInvalid()
    {
        const string lang = "rw";
        var sectionId = Guid.NewGuid();
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = sectionId, Title = "Section Title", Content = "Original content" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();
        var initiateResult = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 0, translatedText = "   " }
        });
        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: editsJson);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowSubmissionInvalid);

        await AssertNoWritesOccurred(TalkId, lang, "Original content");
    }

    // Gate 4 — a submitted section containing a <script> tag is rejected (coarse XSS denylist).
    [Fact]
    public async Task SubmitExternalReview_SectionWithScriptTag_Returns400WithSubmissionInvalid()
    {
        const string lang = "sn";
        var sectionId = Guid.NewGuid();
        var originalSectionsJson = JsonSerializer.Serialize(new[]
        {
            new { SectionId = sectionId, Title = "Section Title", Content = "Original content" }
        });
        await SeedToolboxTalkTranslationAsync(TalkId, lang, originalSectionsJson);
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.InternalReviewSubmitted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();
        var initiateResult = await service.InitiateExternalReview(TalkId, lang, "reviewer@example.com");

        var editsJson = JsonSerializer.Serialize(new[]
        {
            new { sectionIndex = 0, translatedText = "Hello <script>alert(1)</script>" }
        });
        var result = await service.SubmitExternalReview(initiateResult.Data!.Token, accepted: true, editedContent: editsJson);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowSubmissionInvalid);

        await AssertNoWritesOccurred(TalkId, lang, "Original content");
    }

    /// <summary>
    /// Asserts a failed validation gate left no trace: TranslatedSections unchanged,
    /// no WorkflowReview row, and no ExternalReviewSubmitted event written.
    /// </summary>
    private async Task AssertNoWritesOccurred(Guid talkId, string languageCode, string expectedOriginalContent)
    {
        var db = GetDbContext();

        var translation = await db.Set<ToolboxTalkTranslation>()
            .IgnoreQueryFilters()
            .FirstAsync(t => t.ToolboxTalkId == talkId && t.LanguageCode == languageCode && !t.IsDeleted);
        var sections = JsonSerializer.Deserialize<List<TranslatedSectionSnapshot>>(translation.TranslatedSections);
        sections![0].Content.Should().Be(expectedOriginalContent, "a failed gate must not modify TranslatedSections");
        translation.LastExternalReviewedAt.Should().BeNull("a failed gate must not stamp provenance");

        var hasReview = await db.Set<WorkflowReview>()
            .IgnoreQueryFilters()
            .AnyAsync(r => r.WorkflowType == WorkflowType.Translation
                        && r.TargetEntityId == talkId
                        && r.TargetEntitySubKey == languageCode);
        hasReview.Should().BeFalse("a failed gate must not write a WorkflowReview row");

        var hasSubmittedEvent = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .AnyAsync(e => e.WorkflowType == WorkflowType.Translation
                        && e.TargetEntityId == talkId
                        && e.TargetEntitySubKey == languageCode
                        && e.EventType == WorkflowEventTypes.ExternalReviewSubmitted);
        hasSubmittedEvent.Should().BeFalse("a failed gate must not write an ExternalReviewSubmitted event");
    }

    // ── Phase 4.6 — FlaggedWordCount in GetState ──────────────────────────────

    // 43 — GetState with a seeded validation run and flags returns the computed FlaggedWordCount.
    //
    //  Result 1: OriginalText = "The quick brown fox jumps over the lazy dog"
    //    Flag A: [4..9)   → "quick"       → 1 word
    //    Flag B: [16..19) → "fox"         → 1 word
    //    Merged (non-overlapping): (4,9) and (16,19) → 1 + 1 = 2 words
    //  Result 2: OriginalText = "Hello world"
    //    Flag C: [0..11)  → "Hello world" → 2 words
    //  Expected total: 1 + 1 + 2 = 4
    [Fact]
    public async Task GetState_WithFlaggedSections_ReturnsFlaggedWordCount()
    {
        const string lang = "ta";

        // Any event gives a non-Initial state; FlaggedWordCount populates regardless of state.
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.TranslationCompleted);

        var runId = await SeedValidationRunAsync(TalkId, lang);

        var result1Id = await SeedValidationResultAsync(runId, sectionIndex: 0,
            originalText: "The quick brown fox jumps over the lazy dog");
        await SeedFlagForLangAsync(result1Id, startOffset: 4,  endOffset: 9,  languageCode: lang); // "quick" → 1 word
        await SeedFlagForLangAsync(result1Id, startOffset: 16, endOffset: 19, languageCode: lang); // "fox"   → 1 word

        var result2Id = await SeedValidationResultAsync(runId, sectionIndex: 1,
            originalText: "Hello world");
        await SeedFlagForLangAsync(result2Id, startOffset: 0, endOffset: 11, languageCode: lang); // "Hello world" → 2 words

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var state = await service.GetState(TalkId, lang);

        state.Should().NotBeNull();
        state.FlaggedWordCount.Should().Be(4);
    }

    // 44 — GetState with no validation run returns FlaggedWordCount = 0
    [Fact]
    public async Task GetState_WithNoValidationRun_ReturnsFlaggedWordCountZero()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var state = await service.GetState(TalkId, "ms");

        state.Should().NotBeNull();
        state.FlaggedWordCount.Should().Be(0);
    }

    // ── System context tests (explicit tenant ID — Hangfire job path) ─────────
    // These tests verify that passing explicitTenantId bypasses the HTTP-context
    // tenant filter. The scenario mirrors a Hangfire job calling the service when
    // HttpContext is null and ICurrentUserService.TenantId returns Guid.Empty.

    /// <summary>
    /// Seeds a WorkflowEvent row with an explicitly set TenantId.
    /// Simulates rows written by the service operating in system (Hangfire) context
    /// with a real tenant GUID rather than the Guid.Empty auto-stamp.
    /// Note: the auto-stamp interceptor only overwrites TenantId when it is Guid.Empty
    /// (see CLAUDE.md note 22), so a non-empty TenantId set before Add() is safe.
    /// </summary>
    private async Task SeedEventWithTenantAsync(Guid talkId, string languageCode, string eventType, Guid tenantId)
    {
        var db = GetDbContext();
        db.Set<WorkflowEvent>().Add(new WorkflowEvent
        {
            TenantId = tenantId,
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            EventType = eventType,
            TriggeredByType = TriggeredByType.System,
            TriggeredByUserId = null,
            PayloadJson = null,
            OccurredAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    // 45 — Guid.Empty passed explicitly as explicitTenantId → guard returns WorkflowInvalidState
    [Fact]
    public async Task WorkflowService_SystemContext_ExplicitTenantIdGuidEmpty_ReturnsResultFail()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartTranslation(TalkId, "zz", explicitTenantId: Guid.Empty);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(FailureCode.WorkflowInvalidState);
    }

    // 46 — RecordTranslationCompleted with a real tenantId writes event row stamped with that tenantId
    [Fact]
    public async Task WorkflowService_SystemContext_RecordTranslationCompleted_WithExplicitTenant_WritesEventWithCorrectTenantId()
    {
        var systemTenantId = Guid.NewGuid();
        const string lang = "zy";

        // Pre-condition: Translating state under systemTenantId (TranslationStarted → Translating)
        await SeedEventWithTenantAsync(TalkId, lang, WorkflowEventTypes.TranslationStarted, systemTenantId);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.RecordTranslationCompleted(TalkId, lang, TriggeredByType.System,
            explicitTenantId: systemTenantId);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == lang
                     && e.EventType == WorkflowEventTypes.TranslationCompleted)
            .ToListAsync();

        events.Should().ContainSingle();
        events[0].TenantId.Should().Be(systemTenantId,
            "AddEvent must stamp TenantId from explicitTenantId, not from HttpContext (which is null in Hangfire)");
    }

    // 47 — RecordValidationCompleted with a real tenantId writes event row stamped with that tenantId
    [Fact]
    public async Task WorkflowService_SystemContext_RecordValidationCompleted_WithExplicitTenant_WritesEventWithCorrectTenantId()
    {
        var systemTenantId = Guid.NewGuid();
        const string lang = "zx";

        // Pre-condition: AIGenerated state under systemTenantId (TranslationCompleted → AIGenerated)
        await SeedEventWithTenantAsync(TalkId, lang, WorkflowEventTypes.TranslationCompleted, systemTenantId);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.RecordValidationCompleted(TalkId, lang, TriggeredByType.System,
            explicitTenantId: systemTenantId);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == lang
                     && e.EventType == WorkflowEventTypes.ValidationCompleted)
            .ToListAsync();

        events.Should().ContainSingle();
        events[0].TenantId.Should().Be(systemTenantId,
            "AddEvent must stamp TenantId from explicitTenantId, not from HttpContext (which is null in Hangfire)");
    }

    // 48 — MarkStale with explicit tenantId writes event row stamped with that tenantId
    [Fact]
    public async Task WorkflowService_SystemContext_MarkStale_WithExplicitTenant_WritesEventWithCorrectTenantId()
    {
        var systemTenantId = Guid.NewGuid();
        const string lang = "zw";

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        // No prior event needed — MarkStale is legal from any state (Initial included)
        var result = await service.MarkStale(TalkId, lang, TriggeredByType.System,
            explicitTenantId: systemTenantId);

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == lang
                     && e.EventType == WorkflowEventTypes.MarkedStale)
            .ToListAsync();

        events.Should().ContainSingle();
        events[0].TenantId.Should().Be(systemTenantId,
            "AddEvent must stamp TenantId from explicitTenantId, not from HttpContext (which is null in Hangfire)");
    }

    // 49 — GetState with explicitTenantId sees only events for that tenant, excluding Guid.Empty rows
    //
    //  Setup:
    //    t₁: TranslationStarted  seeded with TenantId = systemTenantId  → last event for systemTenantId = Translating
    //    t₂: TranslationCompleted seeded with TenantId = Guid.Empty      → would give AIGenerated if cross-tenant bleed occurred
    //
    //  With fix: WHERE TenantId == systemTenantId finds only TranslationStarted → Translating ✓
    //  Without fix: WHERE TenantId == Guid.Empty finds only TranslationCompleted → AIGenerated ✗ (or Initial if neither is found)
    [Fact]
    public async Task WorkflowService_SystemContext_GetState_WithExplicitTenant_ExcludesEventsFromOtherTenants()
    {
        var systemTenantId = Guid.NewGuid();
        const string lang = "zv";

        // t₁: systemTenantId's last event is TranslationStarted → state should be Translating
        await SeedEventWithTenantAsync(TalkId, lang, WorkflowEventTypes.TranslationStarted, systemTenantId);

        // t₂: Guid.Empty tenant event that would bleed through if tenant predicate is missing
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.TranslationCompleted);

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var state = await service.GetState(TalkId, lang, explicitTenantId: systemTenantId);

        state.State.Should().Be(TranslationWorkflowState.Translating,
            "only TranslationStarted belongs to systemTenantId; " +
            "the Guid.Empty TranslationCompleted must be excluded by the explicit TenantId predicate");
    }

    // ── 50-51 — LastValidationRunId populated for in-progress runs ───────────

    // 50 — GetState returns LastValidationRunId when a run is in Running status (not yet complete)
    [Fact]
    public async Task GetState_ReturnsLastValidationRunId_DuringRunningState()
    {
        const string lang = "vi";

        // Pre-condition: Translating state
        await SeedEventAsync(TalkId, lang, WorkflowEventTypes.TranslationStarted);

        // Create a validation run that is Running (job has started but not finished)
        var db = GetDbContext();
        var runId = Guid.NewGuid();
        db.Set<TranslationValidationRun>().Add(new TranslationValidationRun
        {
            Id = runId,
            ToolboxTalkId = TalkId,
            LanguageCode = lang,
            Status = ValidationRunStatus.Running,
            PassThreshold = 75,
            SourceLanguage = "en",
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var state = await service.GetState(TalkId, lang);

        state.Should().NotBeNull();
        state.State.Should().Be(TranslationWorkflowState.Translating);
        state.LastValidationRunId.Should().Be(runId, "in-progress run must be returned regardless of completion status");
        state.LastValidationOutcome.Should().BeNull("outcome is undefined until the run completes");
    }

    // 51 — GetState returns the most-recent run when multiple runs exist in mixed statuses
    [Fact]
    public async Task GetState_LastValidationRunId_ReturnsLatestRunWhenMultipleExist()
    {
        const string lang = "mn";

        var db = GetDbContext();

        // Run A: Failed, earliest StartedAt
        var runAId = Guid.NewGuid();
        var startedA = DateTime.UtcNow.AddMinutes(-30);
        db.Set<TranslationValidationRun>().Add(new TranslationValidationRun
        {
            Id = runAId,
            ToolboxTalkId = TalkId,
            LanguageCode = lang,
            Status = ValidationRunStatus.Failed,
            PassThreshold = 75,
            SourceLanguage = "en",
            StartedAt = startedA,
            CreatedAt = startedA,
            CreatedBy = "test"
        });

        // Run B: Completed, StartedAt later than A
        var runBId = Guid.NewGuid();
        var startedB = DateTime.UtcNow.AddMinutes(-20);
        db.Set<TranslationValidationRun>().Add(new TranslationValidationRun
        {
            Id = runBId,
            ToolboxTalkId = TalkId,
            LanguageCode = lang,
            Status = ValidationRunStatus.Completed,
            OverallOutcome = ValidationOutcome.Pass,
            PassThreshold = 75,
            SourceLanguage = "en",
            StartedAt = startedB,
            CompletedAt = startedB.AddMinutes(5),
            CreatedAt = startedB,
            CreatedBy = "test"
        });

        // Run C: Running, StartedAt is latest
        var runCId = Guid.NewGuid();
        var startedC = DateTime.UtcNow.AddMinutes(-5);
        db.Set<TranslationValidationRun>().Add(new TranslationValidationRun
        {
            Id = runCId,
            ToolboxTalkId = TalkId,
            LanguageCode = lang,
            Status = ValidationRunStatus.Running,
            PassThreshold = 75,
            SourceLanguage = "en",
            StartedAt = startedC,
            CreatedAt = startedC,
            CreatedBy = "test"
        });

        await db.SaveChangesAsync();

        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var state = await service.GetState(TalkId, lang);

        state.Should().NotBeNull();
        state.LastValidationRunId.Should().Be(runCId, "Run C has the latest StartedAt and must be returned");
        state.LastValidationOutcome.Should().BeNull("Run C is Running, not Completed — outcome is undefined");
    }

    // ── Local helper type for asserting TranslatedSections content ────────────

    private sealed class TranslatedSectionSnapshot
    {
        public Guid SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; }
    }
}
