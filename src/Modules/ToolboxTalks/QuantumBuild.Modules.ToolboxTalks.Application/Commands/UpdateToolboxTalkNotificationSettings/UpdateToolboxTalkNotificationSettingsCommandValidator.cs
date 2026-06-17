using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkNotificationSettings;

public class UpdateToolboxTalkNotificationSettingsCommandValidator
    : AbstractValidator<UpdateToolboxTalkNotificationSettingsCommand>
{
    public UpdateToolboxTalkNotificationSettingsCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");
    }
}
