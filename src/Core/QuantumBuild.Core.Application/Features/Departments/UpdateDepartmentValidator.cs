using FluentValidation;
using QuantumBuild.Core.Application.Features.Departments.DTOs;

namespace QuantumBuild.Core.Application.Features.Departments;

public class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentDto>
{
    public UpdateDepartmentValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Department name is required")
            .MaximumLength(100)
            .WithMessage("Department name must not exceed 100 characters");

        RuleFor(x => x.Code)
            .MaximumLength(20)
            .WithMessage("Department code must not exceed 20 characters")
            .When(x => !string.IsNullOrEmpty(x.Code));
    }
}
