using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.SendForReview;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.PreviewSendForReview;

/// <summary>
/// Projects, per language, the failing sections in a talk's most recent validation run,
/// the resolved reviewer (per TenantReviewerConfiguration), and workflow-state eligibility
/// for InitiateExternalReview. Read-only — initiates nothing.
/// </summary>
public record PreviewSendForReviewQuery : IRequest<Result<PreviewSendForReviewDto>>
{
    public Guid TalkId { get; init; }
    public Guid TenantId { get; init; }
}
