using FluentValidation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.CreateToolboxTalk;

public class CreateToolboxTalkCommandValidator : AbstractValidator<CreateToolboxTalkCommand>
{
    public CreateToolboxTalkCommandValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty()
            .WithMessage("TenantId is required.");

        RuleFor(x => x.Title)
            .NotEmpty()
            .WithMessage("Title is required.")
            .MaximumLength(200)
            .WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Description))
            .WithMessage("Description must not exceed 2000 characters.");

        RuleFor(x => x.Frequency)
            .IsInEnum()
            .WithMessage("Frequency must be a valid value.");

        RuleFor(x => x.VideoUrl)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.VideoUrl))
            .WithMessage("Video URL must not exceed 500 characters.");

        RuleFor(x => x.VideoSource)
            .IsInEnum()
            .WithMessage("Video source must be a valid value.");

        // If video URL is provided, video source must be set
        RuleFor(x => x.VideoSource)
            .NotEqual(VideoSource.None)
            .When(x => !string.IsNullOrEmpty(x.VideoUrl))
            .WithMessage("Video source must be specified when a video URL is provided.");

        RuleFor(x => x.AttachmentUrl)
            .MaximumLength(500)
            .When(x => !string.IsNullOrEmpty(x.AttachmentUrl))
            .WithMessage("Attachment URL must not exceed 500 characters.");

        RuleFor(x => x.MinimumVideoWatchPercent)
            .InclusiveBetween(50, 100)
            .WithMessage("Minimum video watch percent must be between 50 and 100.");

        // If quiz is required, passing score must be set and valid
        RuleFor(x => x.PassingScore)
            .NotNull()
            .When(x => x.RequiresQuiz)
            .WithMessage("Passing score is required when quiz is required.");

        RuleFor(x => x.PassingScore)
            .InclusiveBetween(50, 100)
            .When(x => x.RequiresQuiz && x.PassingScore.HasValue)
            .WithMessage("Passing score must be between 50 and 100.");

        // At least one section required if no video
        RuleFor(x => x.Sections)
            .NotEmpty()
            .When(x => string.IsNullOrEmpty(x.VideoUrl))
            .WithMessage("At least one section is required when no video is provided.");

        // If quiz is required, at least one question must be provided
        RuleFor(x => x.Questions)
            .NotEmpty()
            .When(x => x.RequiresQuiz)
            .WithMessage("At least one question is required when quiz is required.");

        // Validate each section
        RuleForEach(x => x.Sections).ChildRules(section =>
        {
            section.RuleFor(s => s.Title)
                .NotEmpty()
                .WithMessage("Section title is required.")
                .MaximumLength(200)
                .WithMessage("Section title must not exceed 200 characters.");

            section.RuleFor(s => s.Content)
                .NotEmpty()
                .WithMessage("Section content is required.");

            section.RuleFor(s => s.SectionNumber)
                .GreaterThan(0)
                .WithMessage("Section number must be greater than 0.");
        });

        // Validate each question
        RuleForEach(x => x.Questions).ChildRules(question =>
        {
            question.RuleFor(q => q.QuestionText)
                .NotEmpty()
                .WithMessage("Question text is required.")
                .MaximumLength(500)
                .WithMessage("Question text must not exceed 500 characters.");

            question.RuleFor(q => q.QuestionType)
                .IsInEnum()
                .WithMessage("Question type must be a valid value.");

            question.RuleFor(q => q.CorrectAnswer)
                .NotEmpty()
                .WithMessage("Correct answer is required.")
                .MaximumLength(500)
                .WithMessage("Correct answer must not exceed 500 characters.");

            question.RuleFor(q => q.QuestionNumber)
                .GreaterThan(0)
                .WithMessage("Question number must be greater than 0.");

            question.RuleFor(q => q.Points)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Points must be 0 or greater.");

            // Multiple choice questions must have options
            question.RuleFor(q => q.Options)
                .NotEmpty()
                .When(q => q.QuestionType == QuestionType.MultipleChoice)
                .WithMessage("Options are required for multiple choice questions.");

            question.RuleFor(q => q.Options)
                .Must(options => options == null || options.Count >= 2)
                .When(q => q.QuestionType == QuestionType.MultipleChoice)
                .WithMessage("Multiple choice questions must have at least 2 options.");

            // For true/false questions, correct answer must be "True" or "False"
            question.RuleFor(q => q.CorrectAnswer)
                .Must(answer => answer.Equals("True", StringComparison.OrdinalIgnoreCase) ||
                               answer.Equals("False", StringComparison.OrdinalIgnoreCase))
                .When(q => q.QuestionType == QuestionType.TrueFalse)
                .WithMessage("True/False questions must have 'True' or 'False' as the correct answer.");
        });
    }
}
