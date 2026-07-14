using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reviewers;

public class UpdateTenantReviewerConfigurationRequestValidator : AbstractValidator<UpdateTenantReviewerConfigurationRequest>
{
    public UpdateTenantReviewerConfigurationRequestValidator()
    {
        RuleFor(x => x.ReviewerEmail)
            .NotEmpty()
            .WithMessage("Reviewer email is required")
            .EmailAddress()
            .WithMessage("Reviewer email must be a valid email address")
            .MaximumLength(256);

        RuleFor(x => x.ReviewerName)
            .MaximumLength(256);
    }
}
