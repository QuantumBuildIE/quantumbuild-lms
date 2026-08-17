using FluentValidation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CreateToolboxTalkSchedule;

public class CreateToolboxTalkScheduleCommandValidator : AbstractValidator<CreateToolboxTalkScheduleCommand>
{
    public CreateToolboxTalkScheduleCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.ToolboxTalkId)
            .NotEmpty()
            .WithMessage("ToolboxTalkId is required.");

        RuleFor(x => x.ScheduledDate)
            .NotEmpty()
            .WithMessage("ScheduledDate is required.")
            .Must(date => date.Date >= DateTime.UtcNow.Date)
            .WithMessage("ScheduledDate must be today or in the future.");

        RuleFor(x => x.Frequency)
            .IsInEnum()
            .WithMessage("Frequency must be a valid value.");

        // EndDate must be after ScheduledDate if provided
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.ScheduledDate)
            .When(x => x.EndDate.HasValue)
            .WithMessage("EndDate must be after ScheduledDate.");

        // EndDate is required for recurring schedules (optional - they can run indefinitely)
        // Not enforcing EndDate requirement for recurring schedules to allow indefinite recurrence

        // Either AssignToAllEmployees must be true OR at least one of EmployeeIds/TargetDepartmentIds/TargetSiteIds must have items
        RuleFor(x => x)
            .Must(x => x.AssignToAllEmployees
                || (x.EmployeeIds != null && x.EmployeeIds.Any())
                || (x.TargetDepartmentIds != null && x.TargetDepartmentIds.Any())
                || (x.TargetSiteIds != null && x.TargetSiteIds.Any()))
            .WithMessage("Either AssignToAllEmployees must be true or at least one employee, department, or location must be selected.");

        // If AssignToAllEmployees is true, EmployeeIds/TargetDepartmentIds/TargetSiteIds should be empty (optional validation)
        RuleFor(x => x.EmployeeIds)
            .Must(ids => ids == null || !ids.Any())
            .When(x => x.AssignToAllEmployees)
            .WithMessage("EmployeeIds should be empty when AssignToAllEmployees is true.");

        RuleFor(x => x.TargetDepartmentIds)
            .Must(ids => ids == null || !ids.Any())
            .When(x => x.AssignToAllEmployees)
            .WithMessage("TargetDepartmentIds should be empty when AssignToAllEmployees is true.");

        RuleFor(x => x.TargetSiteIds)
            .Must(ids => ids == null || !ids.Any())
            .When(x => x.AssignToAllEmployees)
            .WithMessage("TargetSiteIds should be empty when AssignToAllEmployees is true.");

        // Validate EmployeeIds are valid GUIDs
        RuleForEach(x => x.EmployeeIds)
            .NotEmpty()
            .When(x => !x.AssignToAllEmployees)
            .WithMessage("EmployeeId cannot be empty.");

        RuleForEach(x => x.TargetDepartmentIds)
            .NotEmpty()
            .When(x => !x.AssignToAllEmployees)
            .WithMessage("Department ID cannot be empty.");

        RuleForEach(x => x.TargetSiteIds)
            .NotEmpty()
            .When(x => !x.AssignToAllEmployees)
            .WithMessage("Site ID cannot be empty.");

        // Notes length validation
        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrEmpty(x.Notes))
            .WithMessage("Notes must not exceed 1000 characters.");
    }
}
