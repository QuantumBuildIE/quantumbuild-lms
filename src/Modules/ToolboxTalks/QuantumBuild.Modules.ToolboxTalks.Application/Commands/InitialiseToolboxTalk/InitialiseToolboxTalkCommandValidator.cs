using FluentValidation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.InitialiseToolboxTalk;

public class InitialiseToolboxTalkCommandValidator : AbstractValidator<InitialiseToolboxTalkCommand>
{
    private static readonly HashSet<string> AllowedAudienceRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Operator", "Supervisor", "Auditor" };

    public InitialiseToolboxTalkCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required.")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .MaximumLength(20)
            .When(x => !string.IsNullOrEmpty(x.Code))
            .WithMessage("Code must not exceed 20 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Description must not exceed 2000 characters.");

        // Text mode: either SourceText or SourceFileUrl must be present
        RuleFor(x => x.SourceText)
            .NotEmpty()
            .When(x => x.InputMode == InputMode.Text && string.IsNullOrEmpty(x.SourceFileUrl))
            .WithMessage("Source text is required for Text mode when no file is provided.");

        // PDF mode: SourceFileUrl required
        RuleFor(x => x.SourceFileUrl)
            .NotEmpty()
            .When(x => x.InputMode == InputMode.Pdf)
            .WithMessage("A source file URL is required for PDF mode.");

        // Video mode: VideoUrl or SourceFileUrl required
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.VideoUrl) || !string.IsNullOrEmpty(x.SourceFileUrl))
            .When(x => x.InputMode == InputMode.Video)
            .WithMessage("A video URL or uploaded file is required for Video mode.");

        RuleFor(x => x.AudienceRole)
            .Must(r => AllowedAudienceRoles.Contains(r))
            .WithMessage("AudienceRole must be one of: Operator, Supervisor, Auditor.");

        RuleFor(x => x.ReviewerName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.ReviewerName));

        RuleFor(x => x.ReviewerOrg)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.ReviewerOrg));

        RuleFor(x => x.ReviewerRole)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.ReviewerRole));

        RuleFor(x => x.DocumentRef)
            .MaximumLength(100)
            .When(x => !string.IsNullOrEmpty(x.DocumentRef));

        RuleFor(x => x.ClientName)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.ClientName));

        RuleFor(x => x.AuditPurpose)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.AuditPurpose));
    }
}
