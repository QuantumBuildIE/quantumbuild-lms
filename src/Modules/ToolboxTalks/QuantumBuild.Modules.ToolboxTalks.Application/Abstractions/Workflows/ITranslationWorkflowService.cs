using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;

public interface ITranslationWorkflowService
{
    /// <summary>Returns current workflow state + ToolboxTalkTranslation snapshot for the given talk/language pair.</summary>
    Task<TranslationWorkflowStateDto> GetState(Guid talkId, string languageCode, CancellationToken ct = default);

    /// <summary>Returns all workflow events for the given talk/language pair, ordered chronologically.</summary>
    Task<IReadOnlyList<WorkflowEventDto>> GetHistory(Guid talkId, string languageCode, CancellationToken ct = default);

    /// <summary>
    /// Records that an AI translation run was kicked off.
    /// <para>Pass <paramref name="confirmOverwrite"/> = true when overwriting an Accepted translation.</para>
    /// </summary>
    Task<Result> StartTranslation(Guid talkId, string languageCode, bool confirmOverwrite = false, TriggeredByType triggeredBy = TriggeredByType.User, CancellationToken ct = default);

    /// <summary>
    /// Records that an in-flight translation has completed successfully.
    /// Transitions the language's state to AIGenerated. Idempotent —
    /// calling from AIGenerated returns success without writing a new
    /// event. Calling from any other state returns
    /// FailureCode.WorkflowInvalidState.
    /// </summary>
    Task<Result> RecordTranslationCompleted(Guid talkId, string languageCode, TriggeredByType triggeredBy = TriggeredByType.User, CancellationToken ct = default);

    /// <summary>Records that a back-translation validation run was kicked off.</summary>
    Task<Result> StartValidation(Guid talkId, string languageCode, CancellationToken ct = default);

    /// <summary>
    /// Records that a back-translation validation run completed successfully.
    /// Transitions to Validated. Idempotent from Validated+.
    /// Legal source states: Validating, AIGenerated.
    /// </summary>
    Task<Result> RecordValidationCompleted(Guid talkId, string languageCode, TriggeredByType triggeredBy = TriggeredByType.User, CancellationToken ct = default);

    /// <summary>Records an internal reviewer's decision (accept or edit).</summary>
    Task<Result> SubmitInternalReview(Guid talkId, string languageCode, bool accepted, string? editedContent, CancellationToken ct = default);

    /// <summary>
    /// Creates an external participant invitation and records the event.
    /// Returns the raw token (for the invitation URL) and the invitation metadata.
    /// </summary>
    Task<Result<InitiateExternalReviewResult>> InitiateExternalReview(Guid talkId, string languageCode, string invitedEmail, CancellationToken ct = default);

    /// <summary>
    /// Handles a submission from an external reviewer via their invitation token.
    /// Returns domain errors for expired or already-used tokens.
    /// </summary>
    Task<Result> SubmitExternalReview(string token, bool accepted, string? editedContent, CancellationToken ct = default);

    /// <summary>Records an internal user's confirmation of the submitted external review.</summary>
    Task<Result> ConfirmExternalReview(Guid talkId, string languageCode, bool accepted, CancellationToken ct = default);

    /// <summary>
    /// Cancels an outstanding external review invitation, revoking the pending invitation and
    /// reverting state from AwaitingThirdParty back to ReviewerAccepted.
    /// </summary>
    Task<Result> CancelExternalReview(Guid talkId, string languageCode, CancellationToken ct = default);

    /// <summary>
    /// Records an external reviewer's explicit decline of the review invitation via their token.
    /// Reason is mandatory. Returns domain errors for expired, already-used, or state-invalid tokens.
    /// </summary>
    Task<Result> DeclineExternalReview(
        string token,
        string reason,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the portal render context for a given raw invitation token.
    /// Looks up the invitation regardless of status so the portal can render the correct error state
    /// for expired/revoked/used invitations. Returns WorkflowTokenInvalid if the token is unknown.
    /// </summary>
    Task<Result<ExternalReviewPortalDto?>> GetPortalContext(
        string token,
        CancellationToken ct = default);

    /// <summary>Marks the translation as accepted and final.</summary>
    Task<Result> AcceptAsFinal(Guid talkId, string languageCode, CancellationToken ct = default);

    /// <summary>Marks the translation as stale (requires re-translation).</summary>
    Task<Result> MarkStale(Guid talkId, string languageCode, TriggeredByType triggeredBy = TriggeredByType.User, CancellationToken ct = default);
}
