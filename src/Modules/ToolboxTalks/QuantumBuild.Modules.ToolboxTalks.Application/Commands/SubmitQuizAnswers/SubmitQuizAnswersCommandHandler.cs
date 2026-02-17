using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Commands.SubmitQuizAnswers;

public class SubmitQuizAnswersCommandHandler : IRequestHandler<SubmitQuizAnswersCommand, QuizResultDto>
{
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICoreDbContext _coreDbContext;
    private readonly ICurrentUserService _currentUserService;

    public SubmitQuizAnswersCommandHandler(
        IToolboxTalksDbContext dbContext,
        ICoreDbContext coreDbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _coreDbContext = coreDbContext;
        _currentUserService = currentUserService;
    }

    public async Task<QuizResultDto> Handle(SubmitQuizAnswersCommand request, CancellationToken cancellationToken)
    {
        // Get the current user's employee record
        var employee = await _coreDbContext.Employees
            .FirstOrDefaultAsync(e => e.UserId == _currentUserService.UserId &&
                                      e.TenantId == _currentUserService.TenantId &&
                                      !e.IsDeleted,
                                 cancellationToken);

        if (employee == null)
        {
            throw new InvalidOperationException("No employee record found for the current user.");
        }

        // Get the scheduled talk with related data
        var scheduledTalk = await _dbContext.ScheduledTalks
            .Include(st => st.SectionProgress)
            .Include(st => st.QuizAttempts)
            .Include(st => st.ToolboxTalk)
                .ThenInclude(t => t.Sections)
            .Include(st => st.ToolboxTalk)
                .ThenInclude(t => t.Questions)
            .FirstOrDefaultAsync(st => st.Id == request.ScheduledTalkId &&
                                       st.TenantId == _currentUserService.TenantId,
                                 cancellationToken);

        if (scheduledTalk == null)
        {
            throw new InvalidOperationException($"Scheduled talk with ID '{request.ScheduledTalkId}' not found.");
        }

        // Validate the scheduled talk belongs to the current user's employee
        if (scheduledTalk.EmployeeId != employee.Id)
        {
            throw new UnauthorizedAccessException("You are not authorized to access this scheduled talk.");
        }

        // Validate the scheduled talk is not already completed or cancelled
        if (scheduledTalk.Status == ScheduledTalkStatus.Completed)
        {
            throw new InvalidOperationException("This scheduled talk has already been completed.");
        }

        if (scheduledTalk.Status == ScheduledTalkStatus.Cancelled)
        {
            throw new InvalidOperationException("This scheduled talk has been cancelled.");
        }

        // Validate quiz is required for this talk
        if (!scheduledTalk.ToolboxTalk.RequiresQuiz)
        {
            throw new InvalidOperationException("This learning does not require a quiz.");
        }

        // Validate all sections are read
        var allSections = scheduledTalk.ToolboxTalk.Sections.ToList();
        var readSections = scheduledTalk.SectionProgress.Where(p => p.IsRead).Select(p => p.SectionId).ToHashSet();

        if (allSections.Count > 0)
        {
            var unreadSections = allSections.Where(s => !readSections.Contains(s.Id)).ToList();
            if (unreadSections.Any())
            {
                throw new InvalidOperationException($"You must read all sections before taking the quiz. Unread sections: {string.Join(", ", unreadSections.Select(s => s.Title))}");
            }
        }

        // Get all questions for grading
        var allQuestions = scheduledTalk.ToolboxTalk.Questions
            .OrderBy(q => q.QuestionNumber)
            .ToList();

        if (!allQuestions.Any())
        {
            throw new InvalidOperationException("This learning has no quiz questions configured.");
        }

        // Determine which questions to grade:
        // If using question pool, only grade questions that were presented (those with submitted answers)
        var submittedQuestionIds = request.Answers.Keys.ToHashSet();
        var useQuestionPool = scheduledTalk.ToolboxTalk.UseQuestionPool;

        List<ToolboxTalkQuestion> questionsToGrade;
        if (useQuestionPool && submittedQuestionIds.Count > 0)
        {
            // Only grade the questions that were presented to the employee
            questionsToGrade = allQuestions
                .Where(q => submittedQuestionIds.Contains(q.Id))
                .ToList();
        }
        else
        {
            questionsToGrade = allQuestions;
        }

        // Grade each answer
        var questionResults = new List<QuestionResultDto>();
        var totalScore = 0;
        var maxScore = 0;

        foreach (var question in questionsToGrade)
        {
            var submittedAnswer = request.Answers.GetValueOrDefault(question.Id, string.Empty);
            var isCorrect = IsAnswerCorrect(question, submittedAnswer);
            var pointsEarned = isCorrect ? question.Points : 0;

            totalScore += pointsEarned;
            maxScore += question.Points;

            questionResults.Add(new QuestionResultDto
            {
                QuestionId = question.Id,
                QuestionNumber = question.QuestionNumber,
                QuestionText = question.QuestionText,
                SubmittedAnswer = submittedAnswer,
                IsCorrect = isCorrect,
                CorrectAnswer = question.CorrectAnswer,
                CorrectOptionIndex = question.CorrectOptionIndex,
                PointsEarned = pointsEarned,
                MaxPoints = question.Points
            });
        }

        // Calculate percentage and determine if passed
        var percentage = maxScore > 0 ? (decimal)totalScore / maxScore * 100 : 0;
        var passingScore = scheduledTalk.ToolboxTalk.PassingScore ?? 80;
        var passed = percentage >= passingScore;

        // Get attempt number
        var attemptNumber = scheduledTalk.QuizAttempts.Count + 1;

        // Build generated questions JSON for audit trail
        string? generatedQuestionsJson = null;
        if (useQuestionPool || scheduledTalk.ToolboxTalk.ShuffleQuestions || scheduledTalk.ToolboxTalk.ShuffleOptions)
        {
            var generatedInfo = questionsToGrade.Select((q, idx) => new
            {
                QuestionId = q.Id,
                OriginalIndex = q.QuestionNumber,
                DisplayIndex = idx + 1
            });
            generatedQuestionsJson = JsonSerializer.Serialize(generatedInfo);
        }

        // Create quiz attempt record
        var quizAttempt = new ScheduledTalkQuizAttempt
        {
            Id = Guid.NewGuid(),
            ScheduledTalkId = scheduledTalk.Id,
            AttemptNumber = attemptNumber,
            Answers = JsonSerializer.Serialize(request.Answers),
            Score = totalScore,
            MaxScore = maxScore,
            Percentage = percentage,
            Passed = passed,
            AttemptedAt = DateTime.UtcNow,
            GeneratedQuestionsJson = generatedQuestionsJson
        };

        _dbContext.ScheduledTalkQuizAttempts.Add(quizAttempt);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new QuizResultDto
        {
            AttemptId = quizAttempt.Id,
            AttemptNumber = attemptNumber,
            Score = totalScore,
            MaxScore = maxScore,
            Percentage = percentage,
            Passed = passed,
            PassingScore = passingScore,
            QuestionResults = questionResults
        };
    }

    private static bool IsAnswerCorrect(ToolboxTalkQuestion question, string submittedAnswer)
    {
        if (string.IsNullOrWhiteSpace(submittedAnswer))
        {
            return false;
        }

        return question.QuestionType switch
        {
            // Case-insensitive comparison for short answer
            QuestionType.ShortAnswer => string.Equals(
                question.CorrectAnswer.Trim(),
                submittedAnswer.Trim(),
                StringComparison.OrdinalIgnoreCase),

            // Case-insensitive comparison for true/false
            QuestionType.TrueFalse => string.Equals(
                question.CorrectAnswer.Trim(),
                submittedAnswer.Trim(),
                StringComparison.OrdinalIgnoreCase),

            // Index-based grading for multiple choice (translation-safe)
            QuestionType.MultipleChoice => IsMultipleChoiceCorrect(question, submittedAnswer),

            _ => false
        };
    }

    private static bool IsMultipleChoiceCorrect(ToolboxTalkQuestion question, string submittedAnswer)
    {
        // Primary: Index-based grading (translation-safe).
        // Frontend sends the original option index as a string (e.g., "0", "1", "2", "3").
        // For shuffled quizzes, the frontend maps display index -> original index before sending.
        if (question.CorrectOptionIndex.HasValue && int.TryParse(submittedAnswer.Trim(), out int selectedIndex))
        {
            return selectedIndex == question.CorrectOptionIndex.Value;
        }

        // Fallback: Text comparison for legacy attempts (submitted before index-based grading)
        return string.Equals(
            question.CorrectAnswer?.Trim(),
            submittedAnswer.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}
