using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

public class GenerateInspectionReportRequestValidator : AbstractValidator<GenerateInspectionReportRequest>
{
    public GenerateInspectionReportRequestValidator()
    {
        RuleFor(x => x.ResponsiblePersonName)
            .NotEmpty()
            .WithMessage("Responsible person name is required")
            .MaximumLength(200);

        RuleFor(x => x.ResponsiblePersonRole)
            .NotEmpty()
            .WithMessage("Responsible person role is required")
            .MaximumLength(200);

        RuleFor(x => x.AuditPurpose)
            .MaximumLength(500)
            .When(x => x.AuditPurpose != null);
    }
}
