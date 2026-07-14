using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.SendForReview;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.SendForReview;

/// <summary>
/// Sends a talk's failing translation sections for external review — one InitiateExternalReview
/// invitation per language with Fail-outcome sections, each scoped to that language's failing
/// sections only. Recomputes PreviewSendForReviewQuery server-side; refuses to initiate anything
/// if any affected language is blocked (no resolved reviewer or ineligible workflow state).
/// </summary>
public record SendForReviewCommand : IRequest<Result<SendForReviewResultDto>>
{
    public Guid TalkId { get; init; }
    public Guid TenantId { get; init; }
}
