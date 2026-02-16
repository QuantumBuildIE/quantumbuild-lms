using FluentValidation;
using QuantumBuild.Core.Application.Features.Sites.DTOs;

namespace QuantumBuild.Core.Application.Features.Sites;

public class UpdateSiteValidator : AbstractValidator<UpdateSiteDto>
{
    public UpdateSiteValidator()
    {
        RuleFor(x => x.SiteCode)
            .NotEmpty()
            .WithMessage("Site code is required")
            .MaximumLength(50)
            .WithMessage("Site code must not exceed 50 characters");

        RuleFor(x => x.SiteName)
            .NotEmpty()
            .WithMessage("Site name is required")
            .MaximumLength(200)
            .WithMessage("Site name must not exceed 200 characters");

        RuleFor(x => x.Address)
            .MaximumLength(500)
            .WithMessage("Address must not exceed 500 characters")
            .When(x => !string.IsNullOrEmpty(x.Address));

        RuleFor(x => x.City)
            .MaximumLength(100)
            .WithMessage("City must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.City));

        RuleFor(x => x.PostalCode)
            .MaximumLength(20)
            .WithMessage("Postal code must not exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.PostalCode));

        RuleFor(x => x.Phone)
            .MaximumLength(50)
            .WithMessage("Phone must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Email)
            .MaximumLength(200)
            .WithMessage("Email must not exceed 200 characters")
            .EmailAddress()
            .WithMessage("Invalid email format")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithMessage("Notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
