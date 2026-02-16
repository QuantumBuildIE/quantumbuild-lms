using FluentValidation;
using QuantumBuild.Core.Application.Features.Users.DTOs;

namespace QuantumBuild.Core.Application.Features.Users;

public class UpdateUserValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name is required")
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name is required")
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters");

        RuleFor(x => x.RoleIds)
            .NotNull()
            .WithMessage("Role IDs must be provided");
    }
}
