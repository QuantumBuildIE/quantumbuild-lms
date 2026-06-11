using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.ParseToolboxTalkContent;

/// <summary>
/// Parses uploaded source content (text, PDF, or video) into ToolboxTalkSection rows.
/// Text and PDF modes run inline; Video mode enqueues a background job and returns immediately.
/// </summary>
public record ParseToolboxTalkContentCommand(
    Guid TalkId,
    Guid TenantId,
    Guid? UserId) : IRequest<Result<ToolboxTalkDto>>;
