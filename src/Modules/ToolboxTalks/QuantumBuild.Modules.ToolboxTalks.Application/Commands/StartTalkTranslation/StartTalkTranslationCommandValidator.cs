using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.StartTalkTranslation;

public class StartTalkTranslationCommandValidator : AbstractValidator<StartTalkTranslationCommand>
{
    public StartTalkTranslationCommandValidator()
    {
        RuleFor(x => x.TalkId).NotEmpty();
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.LanguageCode).NotEmpty().MaximumLength(10);
    }
}
