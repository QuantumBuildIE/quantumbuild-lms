using MediatR;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.PublishToolboxTalk;

/// <summary>
/// Wizard Step 7 — publish a talk directly by talkId (new wizard path).
/// Sets Status = Published, sets PublishedAt, and returns the minimal result.
/// The controller enqueues RequirementMappingJob after this command succeeds.
/// </summary>
public record PublishToolboxTalkCommand(
    Guid TalkId,
    Guid TenantId
) : IRequest<Result<PublishTalkResult>>;

/// <summary>
/// Minimal result returned by the publish endpoint.
/// </summary>
/// <param name="GenerateSlidesFromPdf">
/// Carried through so the controller can decide whether to enqueue slideshow
/// generation (Shape D) without a second DB read — see ToolboxTalksController.PublishByTalkId.
/// </param>
public record PublishTalkResult(
    Guid TalkId,
    string Status,
    DateTime PublishedAt,
    bool GenerateSlidesFromPdf
);
