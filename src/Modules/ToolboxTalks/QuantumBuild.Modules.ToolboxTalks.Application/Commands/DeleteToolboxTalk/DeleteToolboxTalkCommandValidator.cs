using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.DeleteToolboxTalk;

public class DeleteToolboxTalkCommandValidator : AbstractValidator<DeleteToolboxTalkCommand>
{
    public DeleteToolboxTalkCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required.");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");
    }
}
