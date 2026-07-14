using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkSections;

/// <summary>
/// Updates the sections of a wizard-drafted ToolboxTalk without touching any
/// other fields. Used by Step 2 (Parse) of the new learning-wizard.
/// No stalening logic — translations do not exist at this stage.
/// </summary>
public record UpdateToolboxTalkSectionsCommand(
    Guid TalkId,
    Guid TenantId,
    List<UpdateToolboxTalkSectionDto> Sections) : IRequest<Result<ToolboxTalkDto>>;
