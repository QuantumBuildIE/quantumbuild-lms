using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Selects the highest-scoring back-translation, with provider-order tiebreaking (A > B > C > D).
/// </summary>
public class BackTranslationSelector : IBackTranslationSelector
{
    // Ordered provider labels matching back-translation slots A, B, C, D
    private static readonly string[] ProviderLabels = ["Claude Haiku", "DeepL", "Gemini", "Claude Sonnet"];

    /// <inheritdoc />
    public BackTranslationSelection Select(TranslationValidationResult result)
    {
        var candidates = new List<(string Text, int Score, int ProviderIndex)>(4);

        if (result.BackTranslationA != null)
            candidates.Add((result.BackTranslationA, result.ScoreA, 0));
        if (result.BackTranslationB != null)
            candidates.Add((result.BackTranslationB, result.ScoreB, 1));
        if (result.BackTranslationC != null)
            candidates.Add((result.BackTranslationC, result.ScoreC!.Value, 2));
        if (result.BackTranslationD != null)
            candidates.Add((result.BackTranslationD, result.ScoreD!.Value, 3));

        if (candidates.Count == 0)
            throw new InvalidOperationException(
                "No back-translations are available on this result. " +
                "BackTranslationSelector requires at least one non-null back-translation.");

        // Highest score wins; ties broken by ascending provider index (A wins over B over C over D)
        var best = candidates.OrderByDescending(c => c.Score).ThenBy(c => c.ProviderIndex).First();

        return new BackTranslationSelection(best.Text, best.Score, ProviderLabels[best.ProviderIndex]);
    }
}
