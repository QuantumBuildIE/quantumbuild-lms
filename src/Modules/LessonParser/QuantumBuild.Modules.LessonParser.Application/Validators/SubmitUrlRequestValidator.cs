using FluentValidation;
using QuantumBuild.Modules.LessonParser.Application.DTOs;

namespace QuantumBuild.Modules.LessonParser.Application.Validators;

public class SubmitUrlRequestValidator : AbstractValidator<SubmitUrlRequest>
{
    public SubmitUrlRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty()
            .WithMessage("URL is required")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("URL must be a valid absolute HTTP or HTTPS URL");
    }
}
