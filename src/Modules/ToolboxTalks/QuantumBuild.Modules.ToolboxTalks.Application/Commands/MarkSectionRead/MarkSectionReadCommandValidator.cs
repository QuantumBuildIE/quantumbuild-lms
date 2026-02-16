using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.MarkSectionRead;

public class MarkSectionReadCommandValidator : AbstractValidator<MarkSectionReadCommand>
{
    public MarkSectionReadCommandValidator()
    {
        RuleFor(x => x.ScheduledTalkId)
            .NotEmpty()
            .WithMessage("ScheduledTalkId is required.");

        RuleFor(x => x.SectionId)
            .NotEmpty()
            .WithMessage("SectionId is required.");

        RuleFor(x => x.TimeSpentSeconds)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TimeSpentSeconds.HasValue)
            .WithMessage("TimeSpentSeconds must be 0 or greater.");
    }
}
