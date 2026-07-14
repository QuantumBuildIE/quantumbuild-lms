using MediatR;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.StartTalkTranslation;

/// <summary>
/// Starts a translation + validation run for a single language on a new-wizard talk.
/// Creates a TranslationValidationRun (IsNewWizard=true), records TranslationStarted workflow event,
/// and enqueues TranslationValidationJob which generates the translation and validates it.
/// </summary>
public record StartTalkTranslationCommand : IRequest<Result<StartTalkTranslationResult>>
{
    public Guid TalkId { get; init; }
    public Guid TenantId { get; init; }
    public string LanguageCode { get; init; } = string.Empty;
    public bool ConfirmOverwrite { get; init; }
}

public record StartTalkTranslationResult(Guid RunId, string JobId);
