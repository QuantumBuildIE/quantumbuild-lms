using MediatR;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateToolboxTalkQuiz;

/// <summary>
/// Generates AI quiz questions from the talk's existing sections and materialises them as
/// ToolboxTalkQuestion rows. Replaces any previously generated questions atomically.
/// </summary>
public record GenerateToolboxTalkQuizCommand(
    Guid TalkId,
    Guid TenantId,
    Guid? UserId) : IRequest<Result<ToolboxTalkDto>>;
