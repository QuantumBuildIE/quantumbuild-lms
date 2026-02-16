using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateVideoProgress;

public class UpdateVideoProgressCommandValidator : AbstractValidator<UpdateVideoProgressCommand>
{
    public UpdateVideoProgressCommandValidator()
    {
        RuleFor(x => x.ScheduledTalkId)
            .NotEmpty()
            .WithMessage("ScheduledTalkId is required.");

        RuleFor(x => x.WatchPercent)
            .InclusiveBetween(0, 100)
            .WithMessage("WatchPercent must be between 0 and 100.");
    }
}
