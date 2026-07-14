using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Workflows;
using QuantumBuild.Modules.ToolboxTalks.Application.Common;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.SendForReview;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.PreviewSendForReview;

public class PreviewSendForReviewQueryHandler : IRequestHandler<PreviewSendForReviewQuery, Result<PreviewSendForReviewDto>>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly ITranslationWorkflowService _workflowService;

    public PreviewSendForReviewQueryHandler(IToolboxTalksDbContext context, ITranslationWorkflowService workflowService)
    {
        _context = context;
        _workflowService = workflowService;
    }

    public async Task<Result<PreviewSendForReviewDto>> Handle(PreviewSendForReviewQuery request, CancellationToken cancellationToken)
    {
        var talkExists = await _context.ToolboxTalks
            .AnyAsync(t => t.Id == request.TalkId && t.TenantId == request.TenantId && !t.IsDeleted, cancellationToken);

        if (!talkExists)
            return Result.Fail<PreviewSendForReviewDto>("Learning not found.");

        // Most recent run per language — same "no IsCurrent flag, derive from CreatedAt" approach
        // as GetToolboxTalksQueryHandler.GetValidationFailStats.
        var runMeta = await _context.TranslationValidationRuns
            .Where(r => r.ToolboxTalkId == request.TalkId && r.TenantId == request.TenantId && !r.IsDeleted)
            .Select(r => new { r.Id, r.LanguageCode, r.CreatedAt })
            .ToListAsync(cancellationToken);

        if (runMeta.Count == 0)
            return Result.Ok(new PreviewSendForReviewDto { TalkId = request.TalkId, Languages = Array.Empty<PreviewLanguageDto>(), Blocked = false });

        var latestRuns = runMeta
            .GroupBy(r => r.LanguageCode)
            .Select(g => g.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).First())
            .ToList();

        var latestRunIds = latestRuns.Select(r => r.Id).ToList();

        var failingSections = await _context.TranslationValidationResults
            .Where(res => latestRunIds.Contains(res.ValidationRunId) && res.Outcome == ValidationOutcome.Fail)
            .Select(res => new { res.ValidationRunId, res.SectionIndex })
            .ToListAsync(cancellationToken);

        var failingIndicesByRun = failingSections
            .GroupBy(r => r.ValidationRunId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.SectionIndex).OrderBy(i => i).ToList());

        var languagesWithFailures = latestRuns
            .Where(r => failingIndicesByRun.ContainsKey(r.Id))
            .OrderBy(r => r.LanguageCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (languagesWithFailures.Count == 0)
            return Result.Ok(new PreviewSendForReviewDto { TalkId = request.TalkId, Languages = Array.Empty<PreviewLanguageDto>(), Blocked = false });

        var reviewerConfigs = await _context.TenantReviewerConfigurations
            .Where(c => c.TenantId == request.TenantId && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var languageDtos = new List<PreviewLanguageDto>();
        foreach (var run in languagesWithFailures)
        {
            var (config, source) = ReviewerResolution.Resolve(reviewerConfigs, run.LanguageCode);
            var state = await _workflowService.GetState(request.TalkId, run.LanguageCode, ct: cancellationToken);
            var eligible = state.State is TranslationWorkflowState.Validated
                or TranslationWorkflowState.ReviewerAccepted
                or TranslationWorkflowState.ThirdPartyReviewed;

            languageDtos.Add(new PreviewLanguageDto
            {
                LanguageCode = run.LanguageCode,
                FailingSectionIndices = failingIndicesByRun[run.Id],
                FailingSectionCount = failingIndicesByRun[run.Id].Count,
                ResolvedReviewerEmail = config?.ReviewerEmail,
                ResolvedReviewerName = config?.ReviewerName,
                ResolutionSource = source,
                WorkflowStateEligible = eligible
            });
        }

        var blocked = languageDtos.Any(l => l.ResolvedReviewerEmail is null || !l.WorkflowStateEligible);

        return Result.Ok(new PreviewSendForReviewDto
        {
            TalkId = request.TalkId,
            Languages = languageDtos,
            Blocked = blocked
        });
    }
}
