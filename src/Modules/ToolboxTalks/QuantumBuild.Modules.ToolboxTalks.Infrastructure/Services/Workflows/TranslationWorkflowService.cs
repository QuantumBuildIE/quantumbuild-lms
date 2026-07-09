using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Features.TenantSettings;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Translation;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
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
    IToolboxTalkNotificationService notificationService,
    ILogger<TranslationWorkflowService> logger) : ITranslationWorkflowService
{
    // -- Tenant resolution --

    /// <summary>
    /// Resolves the effective tenant ID for a service call.
    /// When <paramref name="explicitTenantId"/> is provided (non-null), it is used directly —
    /// allowing Hangfire jobs (which have no HTTP context) to pass their job-parameter tenant.
    /// When null, falls back to <c>ICurrentUserService.TenantId</c> (standard HTTP-context path).
    /// </summary>
    private Guid ResolveTenantId(Guid? explicitTenantId)
        => explicitTenantId ?? currentUser?.TenantId ?? Guid.Empty;

    /// <summary>
    /// Guard called at the top of every Result-returning public method.
    /// Callers must not pass Guid.Empty as an explicit override — that is a bug at the call site.
    /// Null (default) is always legal.
    /// </summary>
    private Result? ValidateExplicitTenantId(Guid? explicitTenantId)
    {
        if (explicitTenantId.HasValue && explicitTenantId.Value == Guid.Empty)
        {
            logger.LogWarning(
                "TranslationWorkflowService: explicitTenantId = Guid.Empty was passed explicitly. " +
                "This is a caller bug — pass null to fall back to HTTP context, or pass the real tenant ID.");
            return Result.Fail(
                "explicitTenantId must not be Guid.Empty when explicitly provided. Pass null to use the HTTP-context tenant.",
                FailureCode.WorkflowInvalidState);
        }
        return null;
    }

    // -- Public interface --

    public async Task<TranslationWorkflowStateDto> GetState(
        Guid talkId,
        string languageCode,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var tenantId = ResolveTenantId(explicitTenantId);

        // IgnoreQueryFilters: the tenant predicate is applied explicitly so this method is
        // correct regardless of whether HttpContext is present (Hangfire jobs have no HttpContext).
        var lastEvent = await context.WorkflowEvents
            .IgnoreQueryFilters()
            .Where(e => !e.IsDeleted
                     && e.TenantId == tenantId
                     && e.WorkflowType == WorkflowType.Translation
                     && e.TargetEntityId == talkId
                     && e.TargetEntitySubKey == languageCode)
            .OrderByDescending(e => e.OccurredAt)
            .FirstOrDefaultAsync(ct);

        var translation = await context.ToolboxTalkTranslations
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted
                     && t.TenantId == tenantId
                     && t.ToolboxTalkId == talkId
                     && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct);

        // Include runs in any status (Pending, Running, Completed, Failed) so that
        // LastValidationRunId is populated for in-progress runs, not only after completion.
        // Order by StartedAt first (set when the job begins executing), falling back to
        // CreatedAt for Pending runs whose StartedAt is still null.
        var lastValidationRun = await context.TranslationValidationRuns
            .IgnoreQueryFilters()
            .Where(r => !r.IsDeleted
                     && r.TenantId == tenantId
                     && r.ToolboxTalkId == talkId
                     && r.LanguageCode == languageCode)
            .OrderByDescending(r => r.StartedAt ?? r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var state = lastEvent is null
            ? TranslationWorkflowState.Initial
            : EventTypeToState(lastEvent.EventType);

        var flaggedWordCount = await ComputeFlaggedWordCountAsync(talkId, languageCode, tenantId, ct);

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
            LastValidationOutcome = lastValidationRun?.Status == ValidationRunStatus.Completed
                ? lastValidationRun.OverallOutcome
                : null,
            LastValidationRunId = lastValidationRun?.Id,
            FlaggedWordCount = flaggedWordCount,
            LastExternalReviewedAt = translation?.LastExternalReviewedAt,
            LastExternalReviewedBy = translation?.LastExternalReviewedBy,
            SectionReviewStatuses = ParseSectionReviewStatuses(translation?.TranslatedSections)
        };
    }

    /// <summary>
    /// Projects per-section ReviewedAt/ReviewedBy out of the TranslatedSections JSON blob for
    /// read-side consumers (badge, re-translate warning). Returns an empty list rather than
    /// throwing on malformed/absent JSON — this is a display concern, not a write-path guard.
    /// </summary>
    private static IReadOnlyList<SectionReviewStatusDto> ParseSectionReviewStatuses(string? translatedSectionsJson)
    {
        if (string.IsNullOrWhiteSpace(translatedSectionsJson))
            return Array.Empty<SectionReviewStatusDto>();

        List<TranslatedSectionEntry>? sections;
        try
        {
            sections = JsonSerializer.Deserialize<List<TranslatedSectionEntry>>(translatedSectionsJson);
        }
        catch (JsonException)
        {
            return Array.Empty<SectionReviewStatusDto>();
        }

        if (sections is null)
            return Array.Empty<SectionReviewStatusDto>();

        return sections
            .Select((s, index) => new SectionReviewStatusDto
            {
                SectionIndex = index,
                ReviewedAt = s.ReviewedAt,
                ReviewedBy = s.ReviewedBy
            })
            .ToList();
    }

    public async Task<IReadOnlyList<WorkflowEventDto>> GetHistory(
        Guid talkId,
        string languageCode,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var tenantId = ResolveTenantId(explicitTenantId);

        return await context.WorkflowEvents
            .IgnoreQueryFilters()
            .Where(e => !e.IsDeleted
                     && e.TenantId == tenantId
                     && e.WorkflowType == WorkflowType.Translation
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

    public async Task<Result> StartTranslation(
        Guid talkId,
        string languageCode,
        bool confirmOverwrite = false,
        TriggeredByType triggeredBy = TriggeredByType.User,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var guard = ValidateExplicitTenantId(explicitTenantId);
        if (guard is not null) return guard;

        var tenantId = ResolveTenantId(explicitTenantId);
        var stateDto = await GetState(talkId, languageCode, explicitTenantId, ct);
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
            Serialize(new { languageCode, confirmOverwrite }), triggeredBy, tenantId);
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
    public async Task<Result> RecordTranslationCompleted(
        Guid talkId,
        string languageCode,
        TriggeredByType triggeredBy = TriggeredByType.User,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var guard = ValidateExplicitTenantId(explicitTenantId);
        if (guard is not null) return guard;

        var tenantId = ResolveTenantId(explicitTenantId);
        var stateDto = await GetState(talkId, languageCode, explicitTenantId, ct);

        // Idempotent: already in AIGenerated → no-op success
        if (stateDto.State == TranslationWorkflowState.AIGenerated)
            return Result.Ok();

        // Guard: legal source is Translating (started but not yet completed)
        if (stateDto.State != TranslationWorkflowState.Translating)
            return Result.Fail(
                $"Cannot record translation completed from state {stateDto.State}; requires Translating.",
                FailureCode.WorkflowInvalidState);

        AddEvent(talkId, languageCode, WorkflowEventTypes.TranslationCompleted, payloadJson: null, triggeredBy, tenantId);
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result> RecordValidationCompleted(
        Guid talkId,
        string languageCode,
        TriggeredByType triggeredBy = TriggeredByType.User,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var guard = ValidateExplicitTenantId(explicitTenantId);
        if (guard is not null) return guard;

        var tenantId = ResolveTenantId(explicitTenantId);
        var stateDto = await GetState(talkId, languageCode, explicitTenantId, ct);
        var state = stateDto.State;

        // Idempotent: already Validated or further along → no-op success
        if (state is TranslationWorkflowState.Validated
                  or TranslationWorkflowState.ReviewerAccepted
                  or TranslationWorkflowState.AwaitingThirdParty
                  or TranslationWorkflowState.ThirdPartyReviewed
                  or TranslationWorkflowState.Accepted)
            return Result.Ok();

        // Guard: legal source states are Validating (started but not yet completed) or AIGenerated
        if (state is not (TranslationWorkflowState.Validating or TranslationWorkflowState.AIGenerated))
            return Result.Fail(
                $"Cannot record validation completed from state {state}; requires Validating or AIGenerated.",
                FailureCode.WorkflowInvalidState);

        AddEvent(talkId, languageCode, WorkflowEventTypes.ValidationCompleted, payloadJson: null, triggeredBy, tenantId);
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result> StartValidation(
        Guid talkId,
        string languageCode,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var guard = ValidateExplicitTenantId(explicitTenantId);
        if (guard is not null) return guard;

        var tenantId = ResolveTenantId(explicitTenantId);
        var stateDto = await GetState(talkId, languageCode, explicitTenantId, ct);
        var state = stateDto.State;

        // Accept Validating as well as AIGenerated — allows re-entrant calls when
        // the job restarts after a crash mid-run without needing a full state reset.
        if (state is not (TranslationWorkflowState.AIGenerated or TranslationWorkflowState.Validating))
            return Result.Fail(
                $"Cannot start validation from state {state}; requires AIGenerated or Validating.",
                FailureCode.WorkflowInvalidState);

        AddEvent(talkId, languageCode, WorkflowEventTypes.ValidationStarted,
            Serialize(new { languageCode }), TriggeredByType.User, tenantId);
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result> SubmitInternalReview(
        Guid talkId,
        string languageCode,
        bool accepted,
        string? editedContent,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var guard = ValidateExplicitTenantId(explicitTenantId);
        if (guard is not null) return guard;

        var tenantId = ResolveTenantId(explicitTenantId);
        var stateDto = await GetState(talkId, languageCode, explicitTenantId, ct);
        var state = stateDto.State;

        if (state != TranslationWorkflowState.Validated)
            return Result.Fail(
                $"Cannot submit internal review from state {state}; requires Validated.",
                FailureCode.WorkflowInvalidState);

        context.WorkflowReviews.Add(new WorkflowReview
        {
            TenantId = tenantId,
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
            Serialize(new { accepted, hasEditedContent = editedContent is not null }),
            TriggeredByType.User, tenantId);

        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    public async Task<Result<InitiateExternalReviewResult>> InitiateExternalReview(
        Guid talkId,
        string languageCode,
        string invitedEmail,
        List<int>? editableSectionIndices = null,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var guard = ValidateExplicitTenantId(explicitTenantId);
        if (guard is not null) return Result.Fail<InitiateExternalReviewResult>(guard.Errors.FirstOrDefault() ?? "Invalid tenant.", guard.ErrorCode.GetValueOrDefault());

        var tenantId = ResolveTenantId(explicitTenantId);
        var stateDto = await GetState(talkId, languageCode, explicitTenantId, ct);
        var state = stateDto.State;

        if (state is not (TranslationWorkflowState.Validated
                       or TranslationWorkflowState.ReviewerAccepted
                       or TranslationWorkflowState.ThirdPartyReviewed))
            return Result.Fail<InitiateExternalReviewResult>(
                $"Cannot initiate external review from state {state}; requires Validated, ReviewerAccepted, or ThirdPartyReviewed.",
                FailureCode.WorkflowInvalidState);

        var sectionGuard = await ValidateEditableSectionIndicesAsync(talkId, languageCode, editableSectionIndices, ct);
        if (sectionGuard is not null)
            return Result.Fail<InitiateExternalReviewResult>(
                sectionGuard.Errors.FirstOrDefault() ?? "Invalid section selection.",
                sectionGuard.ErrorCode.GetValueOrDefault());

        var rawToken = Guid.NewGuid().ToString("N");
        var tokenHash = HashToken(rawToken);
        var lifetimeRaw = await tenantSettings.GetSettingAsync(
            tenantId,
            TenantSettingKeys.ExternalParticipantTokenLifetimeDays,
            "30",
            ct);
        var lifetimeDays = int.TryParse(lifetimeRaw, out var parsed) && parsed > 0 ? parsed : 30;
        var expiresAt = DateTime.UtcNow.AddDays(lifetimeDays);

        var flaggedWordCount = await ComputeFlaggedWordCountAsync(talkId, languageCode, tenantId, ct);

        var invitation = new ExternalParticipantInvitation
        {
            TenantId = tenantId,
            WorkflowType = WorkflowType.Translation,
            TargetEntityId = talkId,
            TargetEntitySubKey = languageCode,
            InvitedEmail = invitedEmail,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            Status = InvitationStatus.Pending,
            ContextType = "TranslationReview",
            ContextPayload = JsonSerializer.Serialize(new { contextType = "TranslationReview", flaggedWordCount }),
            EditableSectionIndices = editableSectionIndices,
            RequesterUserId = currentUser.UserIdGuid,
            InvitedAt = DateTime.UtcNow
        };
        context.ExternalParticipantInvitations.Add(invitation);

        AddEvent(talkId, languageCode, WorkflowEventTypes.ExternalReviewInitiated,
            Serialize(new { invitedEmail }), TriggeredByType.User, tenantId);

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

        // Auto-apply: on the accept path, the reviewer's edits are validated and merged into the
        // live translation right here — there is no separate admin confirmation step (see
        // docs/external-review-auto-apply-recon.md). Reject/decline submissions are recorded but
        // never touch TranslatedSections, mirroring the old Accepted-only propagation filter.
        List<ExternalReviewEditedSectionDto> edits = new();
        if (accepted)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(editedContent))
                    edits = JsonSerializer.Deserialize<List<ExternalReviewEditedSectionDto>>(editedContent) ?? new();
            }
            catch (JsonException)
            {
                edits = new();
            }

            var validationFailure = await ValidateExternalReviewSubmissionAsync(
                invitation.TargetEntityId, invitation.TargetEntitySubKey ?? string.Empty, edits,
                invitation.EditableSectionIndices, ct);
            if (validationFailure is not null)
                return validationFailure;
        }

        invitation.Status = InvitationStatus.Used;
        invitation.UsedAt = DateTime.UtcNow;

        if (accepted)
        {
            // Gate above already confirmed this row exists, its sections deserialise cleanly, and
            // every submitted SectionIndex is within the invitation's editable scope (or in range,
            // if the invitation carries no restriction).
            var translation = await context.ToolboxTalkTranslations
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.ToolboxTalkId == invitation.TargetEntityId
                                       && t.LanguageCode == invitation.TargetEntitySubKey
                                       && !t.IsDeleted, ct);

            var sections = JsonSerializer.Deserialize<List<TranslatedSectionEntry>>(translation!.TranslatedSections)!;

            var reviewedAt = DateTime.UtcNow;
            foreach (var edit in edits)
            {
                sections[edit.SectionIndex].Content = edit.TranslatedText;
                sections[edit.SectionIndex].ReviewedAt = reviewedAt;
                sections[edit.SectionIndex].ReviewedBy = invitation.InvitedEmail;
            }

            translation.TranslatedSections = JsonSerializer.Serialize(sections);

            // Chunk E: the whole-translation columns are a derived aggregate, not an
            // independent write — they reflect the most recent ReviewedAt across ALL
            // sections (not just this submission's edits), so a scoped round-two submit
            // correctly keeps an earlier round's reviewer as the aggregate if it's still
            // the most recent. Sections without provenance are excluded from the max.
            var mostRecentReviewedSection = sections
                .Where(s => s.ReviewedAt.HasValue)
                .OrderByDescending(s => s.ReviewedAt!.Value)
                .FirstOrDefault();
            if (mostRecentReviewedSection is not null)
            {
                translation.LastExternalReviewedAt = mostRecentReviewedSection.ReviewedAt;
                translation.LastExternalReviewedBy = mostRecentReviewedSection.ReviewedBy;
            }
        }

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

        // Hook 3 — notify tenant admins that an external review response has arrived
        var talkTitle = await context.ToolboxTalks.IgnoreQueryFilters()
            .Where(t => t.Id == invitation.TargetEntityId)
            .Select(t => t.Title)
            .FirstOrDefaultAsync(ct) ?? "Unknown";
        var langName = await languageCodeService.GetLanguageNameAsync(invitation.TargetEntitySubKey ?? string.Empty);
        await notificationService.NotifyExternalReviewResponseAsync(
            invitation.TenantId, invitation.TargetEntityId, talkTitle, langName, accepted, ct);

        return Result.Ok();
    }

    public async Task<Result> CancelExternalReview(
        Guid talkId,
        string languageCode,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var guard = ValidateExplicitTenantId(explicitTenantId);
        if (guard is not null) return guard;

        var tenantId = ResolveTenantId(explicitTenantId);
        var stateDto = await GetState(talkId, languageCode, explicitTenantId, ct);
        var state = stateDto.State;

        if (state != TranslationWorkflowState.AwaitingThirdParty)
            return Result.Fail(
                $"Cannot cancel external review from state {state}; requires AwaitingThirdParty.",
                FailureCode.WorkflowInvalidState);

        // IgnoreQueryFilters + explicit tenant: invitation lookup must work in Hangfire context
        var invitation = await context.ExternalParticipantInvitations
            .IgnoreQueryFilters()
            .Where(i => !i.IsDeleted
                     && i.TenantId == tenantId
                     && i.WorkflowType == WorkflowType.Translation
                     && i.TargetEntityId == talkId
                     && i.TargetEntitySubKey == languageCode
                     && i.Status == InvitationStatus.Pending)
            .FirstOrDefaultAsync(ct);

        if (invitation is null)
            return Result.Fail(
                "No active invitation found for this language.",
                FailureCode.WorkflowInvitationNotFound);

        invitation.Status = InvitationStatus.Revoked;

        AddEvent(talkId, languageCode, WorkflowEventTypes.ExternalReviewCancelled, payloadJson: null, TriggeredByType.User, tenantId);
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
            Sections = sections,
            EditableSectionIndices = invitation.EditableSectionIndices
        };

        return Result.Ok<ExternalReviewPortalDto?>(dto);
    }

    public async Task<Result> AcceptAsFinal(
        Guid talkId,
        string languageCode,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var guard = ValidateExplicitTenantId(explicitTenantId);
        if (guard is not null) return guard;

        var tenantId = ResolveTenantId(explicitTenantId);
        var stateDto = await GetState(talkId, languageCode, explicitTenantId, ct);
        var state = stateDto.State;

        if (state is not (TranslationWorkflowState.Validated
                       or TranslationWorkflowState.ReviewerAccepted
                       or TranslationWorkflowState.ThirdPartyReviewed))
            return Result.Fail(
                $"Cannot accept as final from state {state}; requires Validated, ReviewerAccepted, or ThirdPartyReviewed.",
                FailureCode.WorkflowInvalidState);

        AddEvent(talkId, languageCode, WorkflowEventTypes.AcceptedAsFinal, payloadJson: null, TriggeredByType.User, tenantId);
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    /// <summary>
    /// Marks the translation as stale (requires re-translation).
    /// <para>Idempotent: if the language is already in Stale state, returns success without writing a new event.</para>
    /// </summary>
    public async Task<Result> MarkStale(
        Guid talkId,
        string languageCode,
        TriggeredByType triggeredBy = TriggeredByType.User,
        Guid? explicitTenantId = null,
        CancellationToken ct = default)
    {
        var guard = ValidateExplicitTenantId(explicitTenantId);
        if (guard is not null) return guard;

        var tenantId = ResolveTenantId(explicitTenantId);
        var stateDto = await GetState(talkId, languageCode, explicitTenantId, ct);
        if (stateDto.State == TranslationWorkflowState.Stale)
            return Result.Ok();

        AddEvent(talkId, languageCode, WorkflowEventTypes.MarkedStale, payloadJson: null, triggeredBy, tenantId);
        await context.SaveChangesAsync(ct);

        // TODO Phase 7: fire WorkflowNotificationTrigger
        return Result.Ok();
    }

    // -- Private helpers --

    /// <summary>
    /// Returns state by ignoring all query filters (for token-based public endpoints that have no JWT).
    /// Used only by <see cref="SubmitExternalReview"/> and <see cref="DeclineExternalReview"/>.
    /// </summary>
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
        WorkflowEventTypes.ValidationStarted       => TranslationWorkflowState.Validating,  // Fixes BACKLOG §10
        WorkflowEventTypes.ValidationCompleted     => TranslationWorkflowState.Validated,
        WorkflowEventTypes.InternalReviewSubmitted => TranslationWorkflowState.ReviewerAccepted,
        WorkflowEventTypes.ExternalReviewInitiated => TranslationWorkflowState.AwaitingThirdParty,
        WorkflowEventTypes.ExternalReviewSubmitted => TranslationWorkflowState.ThirdPartyReviewed,
        WorkflowEventTypes.ExternalReviewRejected   => TranslationWorkflowState.ReviewerAccepted,
        WorkflowEventTypes.ExternalReviewCancelled  => TranslationWorkflowState.ReviewerAccepted,
        WorkflowEventTypes.ExternalReviewDeclined   => TranslationWorkflowState.ReviewerAccepted,
        WorkflowEventTypes.AcceptedAsFinal          => TranslationWorkflowState.Accepted,
        WorkflowEventTypes.MarkedStale             => TranslationWorkflowState.Stale,
        _ => TranslationWorkflowState.Initial
    };

    /// <summary>
    /// Appends a workflow event to the context's change tracker.
    /// <paramref name="tenantId"/> is set explicitly on the entity rather than relying on the
    /// DbContext's auto-stamp interceptor, which reads from <c>ICurrentUserService</c> and returns
    /// <c>Guid.Empty</c> when there is no HTTP context (e.g. Hangfire jobs).
    /// </summary>
    private void AddEvent(
        Guid talkId,
        string languageCode,
        string eventType,
        string? payloadJson,
        TriggeredByType triggeredBy = TriggeredByType.User,
        Guid tenantId = default)
    {
        context.WorkflowEvents.Add(new WorkflowEvent
        {
            TenantId = tenantId,
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

    private async Task<int> ComputeFlaggedWordCountAsync(Guid talkId, string languageCode, Guid tenantId, CancellationToken ct)
    {
        // IgnoreQueryFilters + explicit tenant: correct in both HTTP and Hangfire contexts
        var run = await context.TranslationValidationRuns
            .IgnoreQueryFilters()
            .Where(r => !r.IsDeleted
                     && r.TenantId == tenantId
                     && r.ToolboxTalkId == talkId
                     && r.LanguageCode == languageCode
                     && r.Status == ValidationRunStatus.Completed)
            .OrderByDescending(r => r.CompletedAt)
            .FirstOrDefaultAsync(ct);

        if (run is null)
            return 0;

        // TranslationValidationResults are BaseEntity (no TenantId filter).
        // IgnoreQueryFilters on this query also bypasses the Flags (TenantEntity) query filter
        // via Include, which is correct: flags are already scoped to the correct tenant through
        // their ValidationResultId → run → tenant chain.
        var results = await context.TranslationValidationResults
            .IgnoreQueryFilters()
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

    /// <summary>
    /// Validates an external reviewer's accept-path submission before anything is written.
    /// Runs four gates in order, failing fast on the first violation. Only called when
    /// <c>accepted == true</c> — the decline/reject path never propagates edits and has
    /// no section content to validate.
    /// </summary>
    /// <param name="editableSectionIndices">
    /// The invitation's <see cref="ExternalParticipantInvitation.EditableSectionIndices"/>. Null
    /// means the invitation carries no restriction (full-scope review, including every legacy
    /// invitation row) — Gate 2 falls back to validating submitted indices are merely in range
    /// against the live translation. Non-null means every submitted index must be a member of
    /// this set.
    /// </param>
    private async Task<Result?> ValidateExternalReviewSubmissionAsync(
        Guid talkId,
        string languageCode,
        List<ExternalReviewEditedSectionDto> edits,
        List<int>? editableSectionIndices,
        CancellationToken ct)
    {
        // Gate 1 — non-empty submission
        if (edits.Count == 0)
            return Result.Fail(
                "A submission must include at least one section.",
                FailureCode.WorkflowSubmissionInvalid);

        // Gate 2 — every SectionIndex must be within the invitation's editable scope. Loads the
        // live translation regardless of scope, since range/malformed checks apply either way.
        var translation = await context.ToolboxTalkTranslations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ToolboxTalkId == talkId
                                   && t.LanguageCode == languageCode
                                   && !t.IsDeleted, ct);

        if (translation is null)
            return Result.Fail(
                "No translation exists for this language; cannot validate submission.",
                FailureCode.WorkflowSubmissionInvalid);

        List<TranslatedSectionEntry>? sections;
        try
        {
            sections = JsonSerializer.Deserialize<List<TranslatedSectionEntry>>(translation.TranslatedSections);
        }
        catch (JsonException)
        {
            sections = null;
        }

        if (sections is null)
            return Result.Fail(
                "Existing translation content is malformed; cannot validate submission.",
                FailureCode.WorkflowSubmissionInvalid);

        if (editableSectionIndices is null)
        {
            if (edits.Any(e => e.SectionIndex < 0 || e.SectionIndex >= sections.Count))
                return Result.Fail(
                    "One or more sections do not match the current translation content. The reviewer's submission may be out of date.",
                    FailureCode.WorkflowSubmissionInvalid);
        }
        else
        {
            var editableSet = editableSectionIndices.ToHashSet();
            if (edits.Any(e => !editableSet.Contains(e.SectionIndex)))
                return Result.Fail(
                    "One or more edits were submitted for sections not selected for review.",
                    FailureCode.WorkflowSubmissionInvalid);
        }

        // Gate 3 — every submitted section's text must be non-empty
        if (edits.Any(e => string.IsNullOrWhiteSpace(e.TranslatedText)))
            return Result.Fail(
                "One or more sections cannot be left blank.",
                FailureCode.WorkflowSubmissionInvalid);

        // Gate 4 — coarse XSS denylist (not full HTML sanitisation)
        if (edits.Any(e => ContainsDisallowedMarkup(e.TranslatedText)))
            return Result.Fail(
                "One or more sections contain disallowed markup.",
                FailureCode.WorkflowSubmissionInvalid);

        return null;
    }

    /// <summary>
    /// Validates the admin's section selection at initiation time. Null is always legal
    /// (full-scope review, no gates run). Non-null must be non-empty, contain no duplicate
    /// indices, and every index must be in range against the current translation's section
    /// count.
    /// </summary>
    private async Task<Result?> ValidateEditableSectionIndicesAsync(
        Guid talkId,
        string languageCode,
        List<int>? editableSectionIndices,
        CancellationToken ct)
    {
        if (editableSectionIndices is null)
            return null;

        if (editableSectionIndices.Count == 0)
            return Result.Fail(
                "At least one section must be selected for review.",
                FailureCode.WorkflowInitiationInvalid);

        if (editableSectionIndices.Count != editableSectionIndices.Distinct().Count())
            return Result.Fail(
                "Section selection contains duplicate indices.",
                FailureCode.WorkflowInitiationInvalid);

        var translation = await context.ToolboxTalkTranslations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ToolboxTalkId == talkId
                                   && t.LanguageCode == languageCode
                                   && !t.IsDeleted, ct);

        if (translation is null)
            return Result.Fail(
                "No translation exists for this language; cannot select sections for review.",
                FailureCode.WorkflowInitiationInvalid);

        List<TranslatedSectionEntry>? sections;
        try
        {
            sections = JsonSerializer.Deserialize<List<TranslatedSectionEntry>>(translation.TranslatedSections);
        }
        catch (JsonException)
        {
            sections = null;
        }

        if (sections is null)
            return Result.Fail(
                "Existing translation content is malformed; cannot select sections for review.",
                FailureCode.WorkflowInitiationInvalid);

        if (editableSectionIndices.Any(i => i < 0 || i >= sections.Count))
            return Result.Fail(
                "One or more selected sections do not match the current translation content.",
                FailureCode.WorkflowInitiationInvalid);

        return null;
    }

    private static readonly Regex EventHandlerAttributePattern =
        new(@"on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool ContainsDisallowedMarkup(string text)
    {
        if (text.Contains("<script", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.Contains("javascript:", StringComparison.OrdinalIgnoreCase)) return true;
        if (EventHandlerAttributePattern.IsMatch(text)) return true;
        return false;
    }
}
