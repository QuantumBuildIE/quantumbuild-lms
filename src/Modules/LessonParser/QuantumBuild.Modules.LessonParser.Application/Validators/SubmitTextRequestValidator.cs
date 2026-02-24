using FluentValidation;
using QuantumBuild.Modules.LessonParser.Application.DTOs;

namespace QuantumBuild.Modules.LessonParser.Application.Validators;

public class SubmitTextRequestValidator : AbstractValidator<SubmitTextRequest>
{
    public SubmitTextRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .WithMessage("Content is required")
            .MinimumLength(100)
            .WithMessage("Content must be at least 100 characters");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters");
    }
}
