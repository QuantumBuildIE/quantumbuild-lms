using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Selects the best (highest-scoring) back-translation from a validation result.
/// </summary>
public interface IBackTranslationSelector
{
    /// <summary>
    /// Selects the highest-scoring back-translation from the result.
    /// Ties are broken by provider order: A (Claude Haiku) > B (DeepL) > C (Gemini) > D (Claude Sonnet).
    /// </summary>
    /// <param name="result">The validation result containing back-translations and scores.</param>
    /// <returns>The selected back-translation with its score and provider label.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no back-translations are available.</exception>
    BackTranslationSelection Select(TranslationValidationResult result);
}

/// <summary>
/// The selected back-translation with its score and provider label.
/// </summary>
public record BackTranslationSelection(string Text, int Score, string ProviderLabel);
