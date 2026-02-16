using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateContentTranslations;

/// <summary>
/// Validator for GenerateContentTranslationsCommand
/// </summary>
public class GenerateContentTranslationsCommandValidator : AbstractValidator<GenerateContentTranslationsCommand>
{
    public GenerateContentTranslationsCommandValidator()
    {
        RuleFor(x => x.ToolboxTalkId)
            .NotEmpty()
            .WithMessage("Toolbox talk ID is required");

        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("Tenant ID is required");

        RuleFor(x => x.TargetLanguages)
            .NotEmpty()
            .WithMessage("At least one target language is required");

        RuleForEach(x => x.TargetLanguages)
            .NotEmpty()
            .WithMessage("Language name cannot be empty");
    }
}
