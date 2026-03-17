using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Sectors;

public class AssignTenantSectorRequestValidator : AbstractValidator<AssignTenantSectorRequest>
{
    public AssignTenantSectorRequestValidator()
    {
        RuleFor(x => x.SectorId)
            .NotEmpty()
            .WithMessage("Sector ID is required");
    }
}
