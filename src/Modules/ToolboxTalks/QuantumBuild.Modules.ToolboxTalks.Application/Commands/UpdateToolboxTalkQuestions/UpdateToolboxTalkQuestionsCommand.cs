using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkQuestions;

/// <summary>
/// Upserts quiz questions on a wizard-drafted talk without touching other fields.
/// Questions omitted from the list are hard-deleted. Used by Step 3 (Quiz) of the new learning-wizard.
/// </summary>
public record UpdateToolboxTalkQuestionsCommand(
    Guid TalkId,
    Guid TenantId,
    List<UpdateToolboxTalkQuestionDto> Questions) : IRequest<Result<ToolboxTalkDto>>;
