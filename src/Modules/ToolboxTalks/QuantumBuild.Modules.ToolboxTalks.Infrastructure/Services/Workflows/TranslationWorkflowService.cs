using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Features.TenantSettings;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Workflows;

public sealed class TranslationWorkflowService(
    IToolboxTalksDbContext context,
    ICoreDbContext coreContext,
    ICurrentUserService currentUser,
    ITenantSettingsService tenantSettings,
    ILanguageCodeService languageCodeService,
    IEmailService emailService,
    IConfiguration configuration,
    ILogger<TranslationWorkflowService> logger) : ITranslationWorkflowService
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

        var lastValidationRun = await context.TranslationValidationRuns
            .Where(r => r.ToolboxTalkId == talkId
                     && r.LanguageCode == languageCode
                     && r.Status == ValidationRunStatus.Completed)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(ct);

        var state = lastEvent is null
            ? TranslationWorkflowState.Initial
            : EventTypeToState(lastEvent.EventType);

        var flaggedWordCount = await ComputeFlaggedWordCountAsync(talkId, languageCode, ct);

        return new TranslationWorkflowStateDto
        {
            TalkId = talkId,
            LanguageCode = languageCode,
            State = state,
            LastEventType = lastEvent?.EventType,
            LastEventAt = lastEvent?.OccurredAt,
            TranslatedTitle = translation?.TranslatedTitle,
            TranslatedAt = translation?.TranslatedAt,
            NeedsRevalidation = translation?.NeedsRevalidation ?? false,
            LastValidationOutcome = lastValidationRun?.OverallOutcome,
            LastValidationRunId = lastValidationRun?.Id,
            FlaggedWordCount = flaggedWordCount
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

    public async Task<Result> StartTranslation(Guid talkId, string languageCode, bool confirmOverwrite = false, TriggeredByType triggeredBy = TriggeredByType.User, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);
        var state = stateDto.State;

        if (state is TranslationWorkflowState.Translating && !confirmOverwrite)
            return Result.Fail(
                "Cannot start translation: a translation is already in flight for this language. Set confirmOverwrite=true to confirm overwrite of the in-progress translation.",
                FailureCode.WorkflowConfirmationRequired);

        if ((state is TranslationWorkflowState.Accepted or TranslationWorkflowState.ReviewerAccepted) && !confirmOverwrite)
            return Result.Fail(
                $"Cannot start translation: language is in {state} state. Set confirmOverwrite=true to confirm overwrite of accepted translation.",
                FailureCode.WorkflowConfirmationRequired);

        if (state is TranslationWorkflowState.AwaitingThirdParty or TranslationWorkflowState.ThirdPartyReviewed)
            return Result.Fail(
                $"Cannot start translation while external review is in progress (current state: {state}).",
                FailureCode.WorkflowInvalidState);

        AddEvent(talkId, languageCode, WorkflowEventTypes.TranslationStarted,
            Serialize(new { languageCode, confirmOverwrite }), triggeredBy);
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    /// <summary>
    /// Records that an in-flight translation has completed successfully.
    /// Transitions the language's state to AIGenerated. Idempotent —
    /// calling from AIGenerated returns success without writing a new
    /// event. Calling from any other state returns
    /// FailureCode.WorkflowInvalidState.
    /// </summary>
    public async Task<Result> RecordTranslationCompleted(Guid talkId, string languageCode, TriggeredByType triggeredBy = TriggeredByType.User, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);

        // Idempotent: already in AIGenerated → no-op success
        if (stateDto.State == TranslationWorkflowState.AIGenerated)
            return Result.Ok();

        // Guard: legal source is Translating (started but not yet completed)
        if (stateDto.State != TranslationWorkflowState.Translating)
            return Result.Fail(
                $"Cannot record translation completed from state {stateDto.State}; requires Translating.",
                FailureCode.WorkflowInvalidState);

        AddEvent(talkId, languageCode, WorkflowEventTypes.TranslationCompleted, payloadJson: null, triggeredBy);
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
        var lifetimeRaw = await tenantSettings.GetSettingAsync(
            currentUser.TenantId,
            TenantSettingKeys.ExternalParticipantTokenLifetimeDays,
            "30",
            ct);
        var lifetimeDays = int.TryParse(lifetimeRaw, out var parsed) && parsed > 0 ? parsed : 30;
        var expiresAt = DateTime.UtcNow.AddDays(lifetimeDays);

        var flaggedWordCount = await ComputeFlaggedWordCountAsync(talkId, languageCode, ct);

        var invitation = new ExternalParticipantInvitation
        {
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            InvitedEmail = invitedEmail,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            Status = InvitationStatus.Pending,
            ContextType = "TranslationReview",
            ContextPayload = JsonSerializer.Serialize(new { contextType = "TranslationReview", flaggedWordCount }),
            RequesterUserId = currentUser.UserIdGuid,
            InvitedAt = DateTime.UtcNow
        };
        context.ExternalParticipantInvitations.Add(invitation);

        AddEvent(talkId, languageCode, WorkflowEventTypes.ExternalReviewInitiated,
            Serialize(new { invitedEmail }));

        await context.SaveChangesAsync(ct);

        // Fire-and-forget: dispatch invitation email. Failure is non-fatal — the
        // invitation row is already committed. MailerSend retry/resilience improvements
        // are tracked in BACKLOG §5.6.
        try
        {
            var baseUrl = configuration["AppSettings:BaseUrl"]
                ?? "https://quantumbuild-lms-web-production.up.railway.app";
            var portalUrl = $"{baseUrl.TrimEnd('/')}/external-review/{rawToken}";

            var talk = await context.ToolboxTalks.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == talkId && !t.IsDeleted, ct);
            var talkTitle = talk?.Title ?? string.Empty;

            string langName;
            try { langName = await languageCodeService.GetLanguageNameAsync(languageCode); }
            catch { langName = languageCode; }
            if (string.IsNullOrEmpty(langName)) langName = languageCode;

            var user = await coreContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == currentUser.UserIdGuid, ct);
            var requesterName = user?.FullName is { Length: > 0 } n ? n : currentUser.UserName;

            await emailService.SendExternalReviewInvitationEmailAsync(
                invitedEmail, talkTitle, langName, expiresAt, portalUrl, requesterName, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to dispatch external review invitation email to {Email} for talk {TalkId}",
                invitedEmail, talkId);
        }

        // TODO Phase 7: fire WorkflowNotificationTrigger
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
        var currentState = await GetStateIgnoringTenantAsync(
            invitation.TargetEntityId,
            invitation.TargetEntitySubKey,
            ct);
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

        if (accepted)
        {
            await PropagateExternalReviewEditsAsync(talkId, languageCode, ct);
        }

        AddEvent(talkId, languageCode, WorkflowEventTypes.ExternalReviewConfirmed,
            Serialize(new { accepted }));
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result> CancelExternalReview(Guid talkId, string languageCode, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);
        var state = stateDto.State;

        if (state != TranslationWorkflowState.AwaitingThirdParty)
            return Result.Fail(
                $"Cannot cancel external review from state {state}; requires AwaitingThirdParty.",
                FailureCode.WorkflowInvalidState);

        var invitation = await context.ExternalParticipantInvitations
            .Where(i => i.WorkflowType == WorkflowType.Translation
                     && i.TargetEntityId == talkId
                     && i.TargetEntitySubKey == languageCode
                     && i.Status == InvitationStatus.Pending
                     && !i.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (invitation is null)
            return Result.Fail(
                "No active invitation found for this language.",
                FailureCode.WorkflowInvitationNotFound);

        invitation.Status = InvitationStatus.Revoked;

        AddEvent(talkId, languageCode, WorkflowEventTypes.ExternalReviewCancelled, payloadJson: null);
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result> DeclineExternalReview(string token, string reason, CancellationToken ct = default)
    {
        var tokenHash = HashToken(token);

        // IgnoreQueryFilters: public endpoint has no JWT, so tenant filter would block the lookup
        var invitation = await context.ExternalParticipantInvitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && !i.IsDeleted, ct);

        if (invitation is null)
            return Result.Fail("Invitation not found.", FailureCode.WorkflowTokenInvalid);

        if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt < DateTime.UtcNow)
            return Result.Fail("This invitation link has expired.", FailureCode.WorkflowTokenExpired);

        if (string.IsNullOrWhiteSpace(reason))
            return Result.Fail("A reason is required when declining.", FailureCode.WorkflowReasonRequired);

        // State guard: IgnoreQueryFilters since no JWT tenant context
        var currentState = await GetStateIgnoringTenantAsync(
            invitation.TargetEntityId,
            invitation.TargetEntitySubKey,
            ct);
        if (currentState != TranslationWorkflowState.AwaitingThirdParty)
            return Result.Fail(
                $"External review decline not accepted from state {currentState}; invitation may be stale.",
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
            Accepted = false,
            EditedContent = null,
            DeclineReason = reason.Trim(),
            SubmittedAt = DateTime.UtcNow
        });

        // Event written on behalf of the external reviewer: no internal user, tenant from invitation
        context.WorkflowEvents.Add(new WorkflowEvent
        {
            TenantId = invitation.TenantId,
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = invitation.TargetEntityId,
            TargetEntitySubKey = invitation.TargetEntitySubKey,
            EventType = WorkflowEventTypes.ExternalReviewDeclined,
            TriggeredByType = TriggeredByType.User,
            TriggeredByUserId = null,
            PayloadJson = Serialize(new { reason = reason.Trim() }),
            OccurredAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result<ExternalReviewPortalDto?>> GetPortalContext(string token, CancellationToken ct = default)
    {
        var tokenHash = HashToken(token);

        // IgnoreQueryFilters: public endpoint has no JWT; look up regardless of status
        // so the portal can render the correct error state for expired/revoked/used invitations
        var invitation = await context.ExternalParticipantInvitations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash && !i.IsDeleted, ct);

        if (invitation is null)
            return Result.Fail<ExternalReviewPortalDto?>("Invitation not found.", FailureCode.WorkflowTokenInvalid);

        var portalStatus = DerivePortalStatus(invitation);
        var languageCode = invitation.TargetEntitySubKey ?? string.Empty;

        // Look up talk title (IgnoreQueryFilters: public path)
        var talk = await context.ToolboxTalks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == invitation.TargetEntityId && !t.IsDeleted, ct);

        var talkTitle = talk?.Title ?? string.Empty;

        // Look up display name for the language code; fall back to the code itself
        string languageName;
        try
        {
            languageName = await languageCodeService.GetLanguageNameAsync(languageCode);
            if (string.IsNullOrEmpty(languageName))
                languageName = languageCode;
        }
        catch
        {
            languageName = languageCode;
        }

        var flaggedWordCount = ParseFlaggedWordCount(invitation.ContextPayload);

        var sections = new List<ExternalReviewSectionDto>();

        if (portalStatus == "Active")
        {
            // Load latest completed validation run (IgnoreQueryFilters: public path)
            var run = await context.TranslationValidationRuns
                .IgnoreQueryFilters()
                .Where(r => r.ToolboxTalkId == invitation.TargetEntityId
                         && r.LanguageCode == languageCode
                         && r.Status == ValidationRunStatus.Completed
                         && !r.IsDeleted)
                .OrderByDescending(r => r.CompletedAt)
                .FirstOrDefaultAsync(ct);

            if (run is not null)
            {
                var results = await context.TranslationValidationResults
                    .IgnoreQueryFilters()
                    .Where(r => r.ValidationRunId == run.Id)
                    .Include(r => r.Flags)
                    .OrderBy(r => r.SectionIndex)
                    .ToListAsync(ct);

                sections = results.Select(r => new ExternalReviewSectionDto
                {
                    SectionIndex = r.SectionIndex,
                    SectionTitle = r.SectionTitle,
                    OriginalText = r.OriginalText,
                    TranslatedText = r.TranslatedText,
                    Flags = r.Flags.Select(f => new ExternalReviewFlagDto
                    {
                        StartOffset = f.StartOffset,
                        EndOffset = f.EndOffset,
                        Severity = f.Severity.ToString(),
                        Reason = f.Reason
                    }).ToList()
                }).ToList();
            }
        }

        var dto = new ExternalReviewPortalDto
        {
            TalkTitle = talkTitle,
            LanguageCode = languageCode,
            LanguageName = languageName,
            ExpiresAt = invitation.ExpiresAt,
            PortalStatus = portalStatus,
            ContextType = invitation.ContextType,
            FlaggedWordCount = flaggedWordCount,
            Sections = sections
        };

        return Result.Ok<ExternalReviewPortalDto?>(dto);
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
    public async Task<Result> MarkStale(Guid talkId, string languageCode, TriggeredByType triggeredBy = TriggeredByType.User, CancellationToken ct = default)
    {
        var stateDto = await GetState(talkId, languageCode, ct);
        if (stateDto.State == TranslationWorkflowState.Stale)
            return Result.Ok();

        AddEvent(talkId, languageCode, WorkflowEventTypes.MarkedStale, payloadJson: null, triggeredBy);
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    // -- Private helpers --

    private async Task<TranslationWorkflowState> GetStateIgnoringTenantAsync(
        Guid talkId,
        string? languageCode,
        CancellationToken ct)
    {
        var lastEvent = await context.WorkflowEvents
            .IgnoreQueryFilters()
            .Where(e => e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == talkId
                     && e.TargetEntitySubKey == languageCode
                     && !e.IsDeleted)
            .OrderByDescending(e => e.OccurredAt)
            .FirstOrDefaultAsync(ct);

        return lastEvent is null
            ? TranslationWorkflowState.Initial
            : EventTypeToState(lastEvent.EventType);
    }

    private static TranslationWorkflowState EventTypeToState(string eventType) => eventType switch
    {
        WorkflowEventTypes.TranslationStarted      => TranslationWorkflowState.Translating,
        WorkflowEventTypes.TranslationCompleted    => TranslationWorkflowState.AIGenerated,
        WorkflowEventTypes.ValidationCompleted     => TranslationWorkflowState.Validated,
        WorkflowEventTypes.InternalReviewSubmitted => TranslationWorkflowState.ReviewerAccepted,
        WorkflowEventTypes.ExternalReviewInitiated => TranslationWorkflowState.AwaitingThirdParty,
        WorkflowEventTypes.ExternalReviewSubmitted => TranslationWorkflowState.ThirdPartyReviewed,
        WorkflowEventTypes.ExternalReviewConfirmed => TranslationWorkflowState.Accepted,
        WorkflowEventTypes.ExternalReviewRejected   => TranslationWorkflowState.ReviewerAccepted,
        WorkflowEventTypes.ExternalReviewCancelled  => TranslationWorkflowState.ReviewerAccepted,
        WorkflowEventTypes.ExternalReviewDeclined   => TranslationWorkflowState.ReviewerAccepted,
        WorkflowEventTypes.AcceptedAsFinal          => TranslationWorkflowState.Accepted,
        WorkflowEventTypes.MarkedStale             => TranslationWorkflowState.Stale,
        // ValidationStarted falls through to Initial; see BACKLOG §10
        // for the deferred fix. TranslationStarted is handled via the
        // case above.
        _ => TranslationWorkflowState.Initial
    };

    private void AddEvent(Guid talkId, string languageCode, string eventType, string? payloadJson, TriggeredByType triggeredBy = TriggeredByType.User)
    {
        context.WorkflowEvents.Add(new WorkflowEvent
        {
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            EventType = eventType,
            TriggeredByType = triggeredBy,
            TriggeredByUserId = triggeredBy == TriggeredByType.System ? null : NullIfEmpty(currentUser.UserIdGuid),
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

    private static string DerivePortalStatus(ExternalParticipantInvitation invitation)
    {
        if (invitation.Status == InvitationStatus.Used)
            return "Used";
        if (invitation.Status == InvitationStatus.Revoked)
            return "Revoked";
        if (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt < DateTime.UtcNow)
            return "Expired";
        if (invitation.Status == InvitationStatus.Pending)
            return "Active";
        return "Unknown";
    }

    private static int ParseFlaggedWordCount(string? contextPayload)
    {
        if (string.IsNullOrEmpty(contextPayload))
            return 0;
        try
        {
            using var doc = JsonDocument.Parse(contextPayload);
            if (doc.RootElement.TryGetProperty("flaggedWordCount", out var prop) && prop.TryGetInt32(out var count))
                return count;
            return 0;
        }
        catch
        {
            return 0;
        }
    }

    private static Guid? NullIfEmpty(Guid id) => id == Guid.Empty ? null : id;

    private async Task<int> ComputeFlaggedWordCountAsync(Guid talkId, string languageCode, CancellationToken ct)
    {
        var run = await context.TranslationValidationRuns
            .Where(r => r.ToolboxTalkId == talkId
                     && r.LanguageCode == languageCode
                     && r.Status == ValidationRunStatus.Completed)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(ct);

        if (run is null)
            return 0;

        var results = await context.TranslationValidationResults
            .Where(r => r.ValidationRunId == run.Id)
            .Include(r => r.Flags)
            .ToListAsync(ct);

        var total = 0;
        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result.OriginalText))
                continue;

            var spans = result.Flags.Select(f => (Start: f.StartOffset, End: f.EndOffset));
            var merged = MergeSpans(spans);

            foreach (var (start, end) in merged)
                total += CountWordsInRange(result.OriginalText, start, end);
        }

        return total;
    }

    private static List<(int Start, int End)> MergeSpans(IEnumerable<(int Start, int End)> spans)
    {
        var sorted = spans.OrderBy(s => s.Start).ToList();
        var merged = new List<(int Start, int End)>();

        foreach (var span in sorted)
        {
            if (merged.Count == 0 || span.Start > merged[^1].End)
                merged.Add(span);
            else
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, span.End));
        }

        return merged;
    }

    private static int CountWordsInRange(string text, int start, int end)
    {
        var clampedStart = Math.Clamp(start, 0, text.Length);
        var clampedEnd = Math.Clamp(end, 0, text.Length);

        if (clampedStart >= clampedEnd)
            return 0;

        return text[clampedStart..clampedEnd]
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    private async Task PropagateExternalReviewEditsAsync(
        Guid talkId,
        string languageCode,
        CancellationToken ct)
    {
        // (a) Find the most recent accepted external WorkflowReview for this talk + language
        var review = await context.WorkflowReviews
            .IgnoreQueryFilters()
            .Where(r => r.WorkflowType == WorkflowType.Translation
                     && r.TargetEntityId == talkId
                     && r.TargetEntitySubKey == languageCode
                     && r.ReviewerType == ReviewerType.External
                     && r.Accepted
                     && !r.IsDeleted)
            .OrderByDescending(r => r.SubmittedAt)
            .FirstOrDefaultAsync(ct);

        // (b) Nothing to propagate — defensive no-op
        if (review is null)
            return;

        // (c) No edits to merge
        if (string.IsNullOrWhiteSpace(review.EditedContent))
            return;

        // (d) Deserialise the reviewer's edit payload
        List<ExternalReviewEditedSectionDto>? edits;
        try
        {
            edits = JsonSerializer.Deserialize<List<ExternalReviewEditedSectionDto>>(review.EditedContent);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "PropagateExternalReviewEdits: failed to deserialise EditedContent for talk {TalkId} language {LanguageCode}; skipping propagation",
                talkId, languageCode);
            return;
        }

        if (edits is null || edits.Count == 0)
            return;

        // (e) Load the translation row
        var translation = await context.ToolboxTalkTranslations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ToolboxTalkId == talkId
                                   && t.LanguageCode == languageCode
                                   && !t.IsDeleted, ct);

        if (translation is null)
        {
            logger.LogWarning(
                "PropagateExternalReviewEdits: no translation found for talk {TalkId} language {LanguageCode}; skipping propagation",
                talkId, languageCode);
            return;
        }

        // (f) Deserialise TranslatedSections
        List<TranslatedSectionEntry>? sections;
        try
        {
            sections = JsonSerializer.Deserialize<List<TranslatedSectionEntry>>(translation.TranslatedSections);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "PropagateExternalReviewEdits: failed to deserialise TranslatedSections for talk {TalkId} language {LanguageCode}; skipping propagation",
                talkId, languageCode);
            return;
        }

        if (sections is null || sections.Count == 0)
            return;

        // (g) Apply each edit; skip out-of-range indices
        foreach (var edit in edits)
        {
            if (edit.SectionIndex < 0 || edit.SectionIndex >= sections.Count)
            {
                logger.LogWarning(
                    "PropagateExternalReviewEdits: sectionIndex {SectionIndex} is out of range (count={Count}) for talk {TalkId} language {LanguageCode}; skipping entry",
                    edit.SectionIndex, sections.Count, talkId, languageCode);
                continue;
            }

            sections[edit.SectionIndex].Content = edit.TranslatedText;
        }

        // (h) Re-serialise and assign; caller's SaveChangesAsync persists this
        translation.TranslatedSections = JsonSerializer.Serialize(sections);
    }

    private sealed class TranslatedSectionEntry
    {
        public Guid SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
