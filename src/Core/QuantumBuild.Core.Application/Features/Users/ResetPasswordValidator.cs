using FluentValidation;
using QuantumBuild.Core.Application.Features.Users.DTOs;

namespace QuantumBuild.Core.Application.Features.Users;

public class ResetPasswordValidator : AbstractValidator<ResetPasswordDto>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required")
            .MinimumLength(6)
            .WithMessage("New password must be at least 6 characters")
            .MaximumLength(100)
            .WithMessage("New password must not exceed 100 characters");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Confirm password is required")
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match");
    }
}
