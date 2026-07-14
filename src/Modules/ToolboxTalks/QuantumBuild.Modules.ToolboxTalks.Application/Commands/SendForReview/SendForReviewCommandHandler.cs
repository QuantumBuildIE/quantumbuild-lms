using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.SendForReview;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.PreviewSendForReview;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.SendForReview;

public class SendForReviewCommandHandler : IRequestHandler<SendForReviewCommand, Result<SendForReviewResultDto>>
{
    private readonly IMediator _mediator;
    private readonly ITranslationWorkflowService _workflowService;

    public SendForReviewCommandHandler(IMediator mediator, ITranslationWorkflowService workflowService)
    {
        _mediator = mediator;
        _workflowService = workflowService;
    }

    public async Task<Result<SendForReviewResultDto>> Handle(SendForReviewCommand request, CancellationToken cancellationToken)
    {
        // Never trust a client-supplied preview — recompute authoritatively here. The modal's
        // preview is a snapshot for the admin's decision only; this call is the source of truth.
        var previewResult = await _mediator.Send(
            new PreviewSendForReviewQuery { TalkId = request.TalkId, TenantId = request.TenantId },
            cancellationToken);

        if (!previewResult.Success)
            return Result.Fail<SendForReviewResultDto>(previewResult.Errors.FirstOrDefault() ?? "Learning not found.");

        var preview = previewResult.Data!;

        if (preview.Languages.Count == 0)
            return Result.Fail<SendForReviewResultDto>("No failing sections found for this learning; nothing to send for review.");

        if (preview.Blocked)
        {
            var blockedLanguages = preview.Languages
                .Where(l => l.ResolvedReviewerEmail is null || !l.WorkflowStateEligible)
                .Select(l => new BlockedLanguageDto
                {
                    LanguageCode = l.LanguageCode,
                    ReviewerMissing = l.ResolvedReviewerEmail is null,
                    WorkflowStateIneligible = !l.WorkflowStateEligible,
                    CurrentWorkflowState = l.CurrentWorkflowState
                })
                .ToList();

            return new Result<SendForReviewResultDto>
            {
                Success = false,
                ErrorCode = FailureCode.WorkflowInvalidState,
                Errors = new List<string> { "One or more languages cannot be sent for review." },
                Data = new SendForReviewResultDto
                {
                    Success = false,
                    Blocked = true,
                    BlockedLanguages = blockedLanguages
                }
            };
        }

        // Best-effort, not transactional: InitiateExternalReview commits its own SaveChangesAsync
        // and fire-and-forget email per call, so wrapping this loop in a DB transaction would not
        // make it atomic — an email could already be sent for language 1 before a later language's
        // call fails, and rolling back language 1's row afterward would orphan that email/token.
        // Every language here already passed the Blocked check above, so per-language failure at
        // this point only happens on a genuine race (e.g. concurrent re-validation) — reported
        // per-language rather than silently retried or rolled back.
        var languageResults = new List<SendForReviewLanguageResultDto>();
        foreach (var language in preview.Languages)
        {
            var initiateResult = await _workflowService.InitiateExternalReview(
                request.TalkId,
                language.LanguageCode,
                language.ResolvedReviewerEmail!,
                language.FailingSections.Select(f => f.Index).ToList(),
                ct: cancellationToken);

            languageResults.Add(new SendForReviewLanguageResultDto
            {
                LanguageCode = language.LanguageCode,
                Success = initiateResult.Success,
                InvitationId = initiateResult.Data?.InvitationId,
                ErrorMessage = initiateResult.Success ? null : initiateResult.Errors.FirstOrDefault()
            });
        }

        var allSucceeded = languageResults.All(r => r.Success);

        return Result.Ok(new SendForReviewResultDto
        {
            Success = allSucceeded,
            Blocked = false,
            LanguageResults = languageResults
        });
    }
}
