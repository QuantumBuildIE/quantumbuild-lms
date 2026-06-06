using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;

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
    Task<Result> StartTranslation(Guid talkId, string languageCode, bool confirmOverwrite = false, CancellationToken ct = default);

    /// <summary>Records that a back-translation validation run was kicked off.</summary>
    Task<Result> StartValidation(Guid talkId, string languageCode, CancellationToken ct = default);

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

    /// <summary>Marks the translation as accepted and final.</summary>
    Task<Result> AcceptAsFinal(Guid talkId, string languageCode, CancellationToken ct = default);

    /// <summary>Marks the translation as stale (requires re-translation).</summary>
    Task<Result> MarkStale(Guid talkId, string languageCode, CancellationToken ct = default);
}
