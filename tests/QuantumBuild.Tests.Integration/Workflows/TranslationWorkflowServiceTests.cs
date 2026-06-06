using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.Workflows;

[Collection("Integration")]
public class TranslationWorkflowServiceTests : IntegrationTestBase
{
    private static readonly Guid TalkId = TestTenantConstants.ToolboxTalks.Talks.BasicTalk;

    public TranslationWorkflowServiceTests(CustomWebApplicationFactory factory) : base(factory) { }

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

    // 3 — StartTranslation → writes TranslationStarted event
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

    // 4 — StartValidation → writes ValidationStarted event
    [Fact]
    public async Task StartValidation_WritesEvent()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.StartValidation(TalkId, "it");

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "it")
            .ToListAsync();

        events.Should().ContainSingle();
        events[0].EventType.Should().Be(WorkflowEventTypes.ValidationStarted);
        events[0].TriggeredByType.Should().Be(TriggeredByType.User);
    }

    // 5 — SubmitInternalReview → writes event and review row
    [Fact]
    public async Task SubmitInternalReview_WritesEventAndReview()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.SubmitInternalReview(TalkId, "pt", accepted: true, editedContent: "edited");

        result.Success.Should().BeTrue();

        var db = GetDbContext();

        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "pt")
            .ToListAsync();
        events.Should().ContainSingle();
        events[0].EventType.Should().Be(WorkflowEventTypes.InternalReviewSubmitted);

        var reviews = await db.Set<WorkflowReview>()
            .IgnoreQueryFilters()
            .Where(r => r.WorkflowType == WorkflowType.Translation
                     && r.TargetEntityId == TalkId
                     && r.TargetEntitySubKey == "pt")
            .ToListAsync();
        reviews.Should().ContainSingle();
        reviews[0].ReviewerType.Should().Be(ReviewerType.Internal);
        reviews[0].ExternalParticipantInvitationId.Should().BeNull();
        reviews[0].EditedContent.Should().Be("edited");
        reviews[0].Accepted.Should().BeTrue();
    }

    // 6 — InitiateExternalReview → writes event and invitation row; token is hashed at rest
    [Fact]
    public async Task InitiateExternalReview_WritesEventAndInvitation()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.InitiateExternalReview(TalkId, "nl", "reviewer@example.com");

        result.Success.Should().BeTrue();
        var inviteResult = result.Data!;
        inviteResult.Token.Should().NotBeNullOrEmpty();
        inviteResult.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var db = GetDbContext();

        var invitation = await db.Set<ExternalParticipantInvitation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == inviteResult.InvitationId);
        invitation.Should().NotBeNull();
        invitation!.Status.Should().Be(InvitationStatus.Pending);
        invitation.InvitedEmail.Should().Be("reviewer@example.com");
        invitation.TokenHash.Should().NotBeNullOrEmpty();
        invitation.TokenHash.Should().NotBe(inviteResult.Token); // hash ≠ raw token
        invitation.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "nl")
            .ToListAsync();
        events.Should().ContainSingle();
        events[0].EventType.Should().Be(WorkflowEventTypes.ExternalReviewInitiated);
    }

    // 7 — SubmitExternalReview with valid token → writes review, marks invitation used, writes event
    //     This test exercises two methods end-to-end to prove the token round-trip works.
    [Fact]
    public async Task SubmitExternalReview_WithValidToken_WritesEventAndReview()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        // Step 1: create invitation, capture raw token
        var initiateResult = await service.InitiateExternalReview(TalkId, "pl", "reviewer@example.com");
        initiateResult.Success.Should().BeTrue();
        var rawToken = initiateResult.Data!.Token;
        var invitationId = initiateResult.Data.InvitationId;

        // Step 2: external reviewer submits via token
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

    // 8 — ConfirmExternalReview → writes ExternalReviewConfirmed event
    [Fact]
    public async Task ConfirmExternalReview_WritesEvent()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        // Precondition: invite created and external review submitted
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

    // 9 — AcceptAsFinal → writes AcceptedAsFinal event
    [Fact]
    public async Task AcceptAsFinal_WritesEvent()
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ITranslationWorkflowService>();

        var result = await service.AcceptAsFinal(TalkId, "da");

        result.Success.Should().BeTrue();

        var db = GetDbContext();
        var events = await db.Set<WorkflowEvent>()
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == TalkId
                     && e.TargetEntitySubKey == "da")
            .ToListAsync();
        events.Should().ContainSingle();
        events[0].EventType.Should().Be(WorkflowEventTypes.AcceptedAsFinal);
    }

    // 10 — MarkStale → writes MarkedStale event
    //      Phase 1: TriggeredByType is User (AddEvent helper hardcodes it).
    //      Phase 2 TODO: system-triggered stale marking should use TriggeredByType.System.
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
}
