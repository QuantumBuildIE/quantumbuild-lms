using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkSettings;

public class UpdateToolboxTalkSettingsCommandValidator
    : AbstractValidator<UpdateToolboxTalkSettingsCommand>
{
    public UpdateToolboxTalkSettingsCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.MinimumVideoWatchPercent)
            .InclusiveBetween(50, 100)
            .WithMessage("Minimum video watch percentage must be between 50 and 100.");

        RuleFor(x => x.AutoAssignDueDays)
            .InclusiveBetween(1, 90)
            .WithMessage("Auto-assign due days must be between 1 and 90.");

        RuleFor(x => x.RefresherFrequency)
            .IsInEnum().WithMessage("Refresher frequency must be Once, Monthly, Quarterly, or Annually.");
    }
}
