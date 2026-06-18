using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.AddTargetLanguage;

public record AddTargetLanguageCommand(
    Guid ToolboxTalkId,
    Guid TenantId,
    string LanguageCode
) : IRequest<Result<ToolboxTalkDto>>;
