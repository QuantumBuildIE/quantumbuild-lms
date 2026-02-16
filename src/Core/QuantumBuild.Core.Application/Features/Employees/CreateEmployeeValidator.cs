using FluentValidation;
using QuantumBuild.Core.Application.Features.Employees.DTOs;

namespace QuantumBuild.Core.Application.Features.Employees;

public class CreateEmployeeValidator : AbstractValidator<CreateEmployeeDto>
{
    public CreateEmployeeValidator()
    {
        RuleFor(x => x.EmployeeCode)
            .NotEmpty()
            .WithMessage("Employee code is required")
            .MaximumLength(50)
            .WithMessage("Employee code must not exceed 50 characters");

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

        RuleFor(x => x.Email)
            .MaximumLength(200)
            .WithMessage("Email must not exceed 200 characters")
            .EmailAddress()
            .WithMessage("Invalid email format")
            .When(x => !string.IsNullOrEmpty(x.Email));

        RuleFor(x => x.Phone)
            .MaximumLength(50)
            .WithMessage("Phone must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Phone));

        RuleFor(x => x.Mobile)
            .MaximumLength(50)
            .WithMessage("Mobile must not exceed 50 characters")
            .When(x => !string.IsNullOrEmpty(x.Mobile));

        RuleFor(x => x.JobTitle)
            .MaximumLength(100)
            .WithMessage("Job title must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.JobTitle));

        RuleFor(x => x.Department)
            .MaximumLength(100)
            .WithMessage("Department must not exceed 100 characters")
            .When(x => !string.IsNullOrEmpty(x.Department));

        RuleFor(x => x.Notes)
            .MaximumLength(2000)
            .WithMessage("Notes must not exceed 2000 characters")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("End date must be after start date")
            .When(x => x.StartDate.HasValue && x.EndDate.HasValue);
    }
}
