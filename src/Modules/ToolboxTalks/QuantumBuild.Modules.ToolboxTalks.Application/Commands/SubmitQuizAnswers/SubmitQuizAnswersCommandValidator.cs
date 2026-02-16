using FluentValidation;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.SubmitQuizAnswers;

public class SubmitQuizAnswersCommandValidator : AbstractValidator<SubmitQuizAnswersCommand>
{
    public SubmitQuizAnswersCommandValidator()
    {
        RuleFor(x => x.ScheduledTalkId)
            .NotEmpty()
            .WithMessage("ScheduledTalkId is required.");

        RuleFor(x => x.Answers)
            .NotNull()
            .WithMessage("Answers dictionary is required.")
            .NotEmpty()
            .WithMessage("At least one answer must be provided.");

        // Validate each answer key is a valid GUID
        RuleForEach(x => x.Answers.Keys)
            .NotEmpty()
            .WithMessage("Question ID cannot be empty.");

        // Validate each answer value is not empty
        RuleForEach(x => x.Answers.Values)
            .NotEmpty()
            .WithMessage("Answer cannot be empty.");
    }
}
