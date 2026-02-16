using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services;

/// <summary>
/// Service for generating randomized quizzes from a toolbox talk's question pool
/// </summary>
public interface IQuizGenerationService
{
    /// <summary>
    /// Generates a randomized quiz for a toolbox talk based on its randomization settings.
    /// Selects questions from the pool (if UseQuestionPool), shuffles order (if ShuffleQuestions),
    /// and shuffles options (if ShuffleOptions).
    /// </summary>
    RandomizedQuiz GenerateQuiz(ToolboxTalk talk);

    /// <summary>
    /// Validates that the question pool has enough questions for the configured quiz size.
    /// Requires at least 2x the QuizQuestionCount.
    /// </summary>
    bool ValidateQuestionPoolSize(int questionCount, int? quizQuestionCount, bool useQuestionPool, out string? error);
}

/// <summary>
/// Represents a generated quiz instance with selected questions and shuffled orders
/// </summary>
public class RandomizedQuiz
{
    public List<RandomizedQuizQuestion> Questions { get; set; } = new();
}

/// <summary>
/// Represents a single question in a generated quiz, capturing the original question
/// reference and any shuffled option ordering
/// </summary>
public class RandomizedQuizQuestion
{
    /// <summary>
    /// The ID of the original ToolboxTalkQuestion
    /// </summary>
    public Guid QuestionId { get; set; }

    /// <summary>
    /// The original question number in the talk's question list
    /// </summary>
    public int OriginalIndex { get; set; }

    /// <summary>
    /// The position shown to the user (1-based: 1, 2, 3...)
    /// </summary>
    public int DisplayIndex { get; set; }

    /// <summary>
    /// Shuffled option indices mapping original positions to display positions.
    /// For example [2,0,3,1] means: display position 0 shows original option 2, etc.
    /// Empty for non-multiple-choice questions.
    /// </summary>
    public List<int> OptionOrder { get; set; } = new();

    /// <summary>
    /// The display index (0-based) of the correct answer after shuffling.
    /// -1 if not applicable (e.g., short answer questions).
    /// </summary>
    public int CorrectOptionDisplayIndex { get; set; } = -1;
}
