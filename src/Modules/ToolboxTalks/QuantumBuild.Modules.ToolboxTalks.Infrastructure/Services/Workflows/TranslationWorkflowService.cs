using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Workflows;

public sealed class TranslationWorkflowService(
    IToolboxTalksDbContext context,
    ICurrentUserService currentUser) : ITranslationWorkflowService
{
    public async Task<TranslationWorkflowStateDto> GetState(Guid talkId, string languageCode, CancellationToken ct = default)
    {
        var lastEvent = await context.WorkflowEvents
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == talkId
                     && e.TargetEntitySubKey == languageCode)
            .OrderByDescending(e => e.OccurredAt)
            .FirstOrDefaultAsync(ct);

        var translation = await context.ToolboxTalkTranslations
            .Where(t => t.ToolboxTalkId == talkId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct);

        var state = lastEvent is null
            ? TranslationWorkflowState.Initial
            : EventTypeToState(lastEvent.EventType);

        return new TranslationWorkflowStateDto
        {
            TalkId = talkId,
            LanguageCode = languageCode,
            State = state,
            LastEventType = lastEvent?.EventType,
            LastEventAt = lastEvent?.OccurredAt,
            TranslatedTitle = translation?.TranslatedTitle,
            TranslatedAt = translation?.TranslatedAt,
            NeedsRevalidation = translation?.NeedsRevalidation ?? false
        };
    }

    public async Task<IReadOnlyList<WorkflowEventDto>> GetHistory(Guid talkId, string languageCode, CancellationToken ct = default)
    {
        return await context.WorkflowEvents
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == talkId
                     && e.TargetEntitySubKey == languageCode)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new WorkflowEventDto
            {
                EventType = e.EventType,
                TriggeredByType = e.TriggeredByType,
                TriggeredByUserId = e.TriggeredByUserId,
                PayloadJson = e.PayloadJson,
                OccurredAt = e.OccurredAt
            })
            .ToListAsync(ct);
    }

    public async Task<Result> StartTranslation(Guid talkId, string languageCode, bool confirmOverwrite = false, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);
        var state = stateDto.State;

        if ((state is TranslationWorkflowState.Accepted or TranslationWorkflowState.ReviewerAccepted) && !confirmOverwrite)
            return Result.Fail(
                $"Cannot start translation: language is in {state} state. Set confirmOverwrite=true to confirm overwrite of accepted translation.",
                FailureCode.WorkflowConfirmationRequired);

        if (state is TranslationWorkflowState.AwaitingThirdParty or TranslationWorkflowState.ThirdPartyReviewed)
            return Result.Fail(
                $"Cannot start translation while external review is in progress (current state: {state}).",
                FailureCode.WorkflowInvalidState);

        AddEvent(talkId, languageCode, WorkflowEventTypes.TranslationStarted,
            Serialize(new { languageCode, confirmOverwrite }));
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result> StartValidation(Guid talkId, string languageCode, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);
        var state = stateDto.State;

        if (state != TranslationWorkflowState.AIGenerated)
            return Result.Fail(
                $"Cannot start validation from state {state}; requires AIGenerated.",
                FailureCode.WorkflowInvalidState);

        AddEvent(talkId, languageCode, WorkflowEventTypes.ValidationStarted,
            Serialize(new { languageCode }));
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result> SubmitInternalReview(Guid talkId, string languageCode, bool accepted, string? editedContent, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);
        var state = stateDto.State;

        if (state != TranslationWorkflowState.Validated)
            return Result.Fail(
                $"Cannot submit internal review from state {state}; requires Validated.",
                FailureCode.WorkflowInvalidState);

        context.WorkflowReviews.Add(new WorkflowReview
        {
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            ReviewerType = ReviewerType.Internal,
            ReviewerUserId = NullIfEmpty(currentUser.UserIdGuid),
            EditedContent = editedContent,
            Accepted = accepted,
            SubmittedAt = DateTime.UtcNow
        });

        AddEvent(talkId, languageCode, WorkflowEventTypes.InternalReviewSubmitted,
            Serialize(new { accepted, hasEditedContent = editedContent is not null }));

        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result<InitiateExternalReviewResult>> InitiateExternalReview(Guid talkId, string languageCode, string invitedEmail, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);
        var state = stateDto.State;

        if (state != TranslationWorkflowState.ReviewerAccepted)
            return Result.Fail<InitiateExternalReviewResult>(
                $"Cannot initiate external review from state {state}; requires ReviewerAccepted.",
                FailureCode.WorkflowInvalidState);

        var rawToken = Guid.NewGuid().ToString("N");
        var tokenHash = HashToken(rawToken);
        // TODO Phase 4: read token lifetime from tenant settings (TenantSettings.ExternalParticipantTokenLifetimeDays)
        var expiresAt = DateTime.UtcNow.AddDays(30);

        var invitation = new ExternalParticipantInvitation
        {
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            InvitedEmail = invitedEmail,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            Status = InvitationStatus.Pending,
            RequesterUserId = currentUser.UserIdGuid,
            InvitedAt = DateTime.UtcNow
        };
        context.ExternalParticipantInvitations.Add(invitation);

        AddEvent(talkId, languageCode, WorkflowEventTypes.ExternalReviewInitiated,
            Serialize(new { invitedEmail }));

        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger (email dispatch handled separately in Phase 4)
        return Result.Ok(new InitiateExternalReviewResult
        {
            InvitationId = invitation.Id,
            Token = rawToken,
            ExpiresAt = expiresAt
        });
    }

    public async Task<Result> SubmitExternalReview(string token, bool accepted, string? editedContent, CancellationToken ct = default)
    {
        var tokenHash = HashToken(token);

        // IgnoreQueryFilters: public endpoint has no JWT, so tenant filter would block the lookup
        var invitation = await context.ExternalParticipantInvitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && !i.IsDeleted, ct);

        if (invitation is null)
            return Result.Fail("Invitation not found.", FailureCode.WorkflowTokenInvalid);

        if (invitation.Status == InvitationStatus.Used)
            return Result.Fail("This invitation link has already been used.", FailureCode.WorkflowTokenAlreadyUsed);

        if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt < DateTime.UtcNow)
            return Result.Fail("This invitation link has expired.", FailureCode.WorkflowTokenExpired);

        // State guard: public endpoint — IgnoreQueryFilters since no JWT tenant context
        var lastEvent = await context.WorkflowEvents
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == invitation.TargetEntityId
                     && e.TargetEntitySubKey == invitation.TargetEntitySubKey
                     && !e.IsDeleted)
            .OrderByDescending(e => e.OccurredAt)
            .FirstOrDefaultAsync(ct);

        var currentState = lastEvent is null ? TranslationWorkflowState.Initial : EventTypeToState(lastEvent.EventType);
        if (currentState != TranslationWorkflowState.AwaitingThirdParty)
            return Result.Fail(
                $"External review submission not accepted from state {currentState}; invitation may be stale.",
                FailureCode.WorkflowInvalidState);

        invitation.Status = InvitationStatus.Used;
        invitation.UsedAt = DateTime.UtcNow;

        context.WorkflowReviews.Add(new WorkflowReview
        {
            TenantId = invitation.TenantId,
            WorkflowType = invitation.WorkflowType,
            TargetEntityId = invitation.TargetEntityId,
            TargetEntitySubKey = invitation.TargetEntitySubKey,
            ReviewerType = ReviewerType.External,
            ExternalParticipantInvitationId = invitation.Id,
            EditedContent = editedContent,
            Accepted = accepted,
            SubmittedAt = DateTime.UtcNow
        });

        // Event written on behalf of the external reviewer: no internal user, tenant from invitation
        context.WorkflowEvents.Add(new WorkflowEvent
        {
            TenantId = invitation.TenantId,
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = invitation.TargetEntityId,
            TargetEntitySubKey = invitation.TargetEntitySubKey,
            EventType = WorkflowEventTypes.ExternalReviewSubmitted,
            TriggeredByType = TriggeredByType.User,
            TriggeredByUserId = null,
            PayloadJson = Serialize(new { accepted }),
            OccurredAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result> ConfirmExternalReview(Guid talkId, string languageCode, bool accepted, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);
        var state = stateDto.State;

        if (state != TranslationWorkflowState.ThirdPartyReviewed)
            return Result.Fail(
                $"Cannot confirm external review from state {state}; requires ThirdPartyReviewed.",
                FailureCode.WorkflowInvalidState);

        AddEvent(talkId, languageCode, WorkflowEventTypes.ExternalReviewConfirmed,
            Serialize(new { accepted }));
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result> AcceptAsFinal(Guid talkId, string languageCode, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);
        var state = stateDto.State;

        if (state is not (TranslationWorkflowState.Validated
                       or TranslationWorkflowState.ReviewerAccepted
                       or TranslationWorkflowState.ThirdPartyReviewed))
            return Result.Fail(
                $"Cannot accept as final from state {state}; requires Validated, ReviewerAccepted, or ThirdPartyReviewed.",
                FailureCode.WorkflowInvalidState);

        AddEvent(talkId, languageCode, WorkflowEventTypes.AcceptedAsFinal, payloadJson: null);
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    /// <summary>
    /// Marks the translation as stale (requires re-translation).
    /// <para>Idempotent: if the language is already in Stale state, returns success without writing a new event.</para>
    /// </summary>
    public async Task<Result> MarkStale(Guid talkId, string languageCode, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);
        if (stateDto.State == TranslationWorkflowState.Stale)
            return Result.Ok();

        AddEvent(talkId, languageCode, WorkflowEventTypes.MarkedStale, payloadJson: null);
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    // -- Private helpers --

    private static TranslationWorkflowState EventTypeToState(string eventType) => eventType switch
    {
        WorkflowEventTypes.TranslationCompleted    => TranslationWorkflowState.AIGenerated,
        WorkflowEventTypes.ValidationCompleted     => TranslationWorkflowState.Validated,
        WorkflowEventTypes.InternalReviewSubmitted => TranslationWorkflowState.ReviewerAccepted,
        WorkflowEventTypes.ExternalReviewInitiated => TranslationWorkflowState.AwaitingThirdParty,
        WorkflowEventTypes.ExternalReviewSubmitted => TranslationWorkflowState.ThirdPartyReviewed,
        WorkflowEventTypes.ExternalReviewConfirmed => TranslationWorkflowState.Accepted,
        WorkflowEventTypes.ExternalReviewRejected  => TranslationWorkflowState.ReviewerAccepted,
        WorkflowEventTypes.AcceptedAsFinal         => TranslationWorkflowState.Accepted,
        WorkflowEventTypes.MarkedStale             => TranslationWorkflowState.Stale,
        // TODO Phase 2: Started events imply transient in-progress state,
        // not Initial. Phase 2 will introduce in-progress states or compute
        // GetState from the most recent terminal event.
        _ => TranslationWorkflowState.Initial
    };

    private void AddEvent(Guid talkId, string languageCode, string eventType, string? payloadJson)
    {
        context.WorkflowEvents.Add(new WorkflowEvent
        {
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            EventType = eventType,
            TriggeredByType = TriggeredByType.User,
            TriggeredByUserId = NullIfEmpty(currentUser.UserIdGuid),
            PayloadJson = payloadJson,
            OccurredAt = DateTime.UtcNow
        });
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value);

    private static Guid? NullIfEmpty(Guid id) => id == Guid.Empty ? null : id;
}
