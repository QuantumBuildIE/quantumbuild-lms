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

        // Either AssignToAllEmployees must be true OR EmployeeIds must have items
        RuleFor(x => x)
            .Must(x => x.AssignToAllEmployees || (x.EmployeeIds != null && x.EmployeeIds.Any()))
            .WithMessage("Either AssignToAllEmployees must be true or at least one EmployeeId must be provided.");

        // If AssignToAllEmployees is true, EmployeeIds should be empty (optional validation)
        RuleFor(x => x.EmployeeIds)
            .Must(ids => ids == null || !ids.Any())
            .When(x => x.AssignToAllEmployees)
            .WithMessage("EmployeeIds should be empty when AssignToAllEmployees is true.");

        // Validate EmployeeIds are valid GUIDs
        RuleForEach(x => x.EmployeeIds)
            .NotEmpty()
            .When(x => !x.AssignToAllEmployees)
            .WithMessage("EmployeeId cannot be empty.");

        // Notes length validation
        RuleFor(x => x.Notes)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrEmpty(x.Notes))
            .WithMessage("Notes must not exceed 1000 characters.");
    }
}
