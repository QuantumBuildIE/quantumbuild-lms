using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CompleteToolboxTalk;

public class CompleteToolboxTalkCommandValidator : AbstractValidator<CompleteToolboxTalkCommand>
{
    public CompleteToolboxTalkCommandValidator()
    {
        RuleFor(x => x.ScheduledTalkId)
            .NotEmpty()
            .WithMessage("ScheduledTalkId is required.");

        RuleFor(x => x.SignatureData)
            .NotEmpty()
            .WithMessage("Signature is required to complete the learning.");

        RuleFor(x => x.SignedByName)
            .NotEmpty()
            .WithMessage("Signed by name is required.")
            .MaximumLength(200)
            .WithMessage("Signed by name must not exceed 200 characters.");
    }
}
