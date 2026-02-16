using System.Text.Json;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

/// <summary>
/// Generates randomized quizzes from a toolbox talk's question pool
/// </summary>
public class QuizGenerationService : IQuizGenerationService
{
    public RandomizedQuiz GenerateQuiz(ToolboxTalk talk)
    {
        var allQuestions = talk.Questions
            .Where(q => !q.IsDeleted)
            .OrderBy(q => q.QuestionNumber)
            .ToList();

        var result = new RandomizedQuiz();
        List<ToolboxTalkQuestion> selectedQuestions;

        // 1. Select questions (random subset or all)
        if (talk.UseQuestionPool && talk.QuizQuestionCount.HasValue
            && allQuestions.Count > talk.QuizQuestionCount.Value)
        {
            selectedQuestions = allQuestions
                .OrderBy(_ => Random.Shared.Next())
                .Take(talk.QuizQuestionCount.Value)
                .ToList();
        }
        else
        {
            selectedQuestions = allQuestions;
        }

        // 2. Shuffle question order if enabled
        if (talk.ShuffleQuestions)
        {
            selectedQuestions = selectedQuestions
                .OrderBy(_ => Random.Shared.Next())
                .ToList();
        }

        // 3. Build generated quiz with option shuffling
        for (int i = 0; i < selectedQuestions.Count; i++)
        {
            var question = selectedQuestions[i];
            var generated = new RandomizedQuizQuestion
            {
                QuestionId = question.Id,
                OriginalIndex = question.QuestionNumber,
                DisplayIndex = i + 1,
            };

            // Parse option count from JSON
            var options = ParseOptions(question.Options);
            var optionCount = options?.Count ?? 0;
            var optionOrder = Enumerable.Range(0, optionCount).ToList();

            if (talk.ShuffleOptions && optionCount > 0 && question.QuestionType == QuestionType.MultipleChoice)
            {
                optionOrder = optionOrder.OrderBy(_ => Random.Shared.Next()).ToList();
            }

            generated.OptionOrder = optionOrder;

            // Find where the correct answer ended up after shuffling
            if (optionCount > 0 && question.QuestionType == QuestionType.MultipleChoice)
            {
                // Prefer CorrectOptionIndex (translation-safe) over text comparison
                var correctOriginalIndex = question.CorrectOptionIndex
                    ?? options?.FindIndex(o =>
                        string.Equals(o, question.CorrectAnswer, StringComparison.OrdinalIgnoreCase))
                    ?? -1;

                if (correctOriginalIndex >= 0)
                {
                    generated.CorrectOptionDisplayIndex = optionOrder.IndexOf(correctOriginalIndex);
                }
            }

            result.Questions.Add(generated);
        }

        return result;
    }

    public bool ValidateQuestionPoolSize(int questionCount, int? quizQuestionCount, bool useQuestionPool, out string? error)
    {
        error = null;

        if (!useQuestionPool || !quizQuestionCount.HasValue)
            return true;

        if (quizQuestionCount.Value <= 0)
        {
            error = "Quiz question count must be greater than 0 when using question pool.";
            return false;
        }

        var requiredPoolSize = quizQuestionCount.Value * 2;

        if (questionCount < requiredPoolSize)
        {
            error = $"Question pool requires at least {requiredPoolSize} questions " +
                    $"(2x quiz size of {quizQuestionCount.Value}). Currently have {questionCount}.";
            return false;
        }

        return true;
    }

    private static List<string>? ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return null;

        try
        {
            return JsonSerializer.Deserialize<List<string>>(optionsJson);
        }
        catch
        {
            return null;
        }
    }
}
