using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkTenantDefaults;

public class UpdateToolboxTalkTenantDefaultsCommandValidator
    : AbstractValidator<UpdateToolboxTalkTenantDefaultsCommand>
{
    private static readonly string[] ValidFrequencies = ["Once", "Monthly", "Quarterly", "Annually"];

    public UpdateToolboxTalkTenantDefaultsCommandValidator()
    {
        RuleFor(x => x.DefaultMinimumVideoWatchPercent)
            .InclusiveBetween(50, 100)
            .WithMessage("Default minimum video watch percentage must be between 50 and 100.");

        RuleFor(x => x.DefaultAutoAssignDueDays)
            .InclusiveBetween(1, 90)
            .WithMessage("Default auto-assign due days must be between 1 and 90.");

        RuleFor(x => x.DefaultRefresherFrequency)
            .NotEmpty()
            .Must(v => ValidFrequencies.Contains(v))
            .WithMessage("Default refresher frequency must be Once, Monthly, Quarterly, or Annually.");
    }
}
