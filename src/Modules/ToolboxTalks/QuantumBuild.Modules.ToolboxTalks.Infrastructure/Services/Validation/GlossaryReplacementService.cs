using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Applies mandatory glossary hard-block replacements to translated text.
/// For each GlossaryMatch that carries an approved translation, attempts a
/// case-insensitive regex replacement of the English source term (and common
/// near-miss variants) in the translated text. Applied BEFORE consensus scoring
/// so the corrected text is what gets back-translated and scored.
/// </summary>
public class GlossaryReplacementService(ILogger<GlossaryReplacementService> logger)
    : IGlossaryReplacementService
{
    public GlossaryReplacementResult Apply(string text, IEnumerable<GlossaryMatch> matches)
    {
        var corrections = new List<GlossaryCorrection>();
        var currentText = text;

        foreach (var match in matches)
        {
            // Only replace when we have an approved translation to apply
            if (string.IsNullOrWhiteSpace(match.ExpectedTranslation))
                continue;

            foreach (var variant in BuildVariants(match.EnglishTerm))
            {
                var pattern = BuildPattern(variant);
                var m = pattern.Match(currentText);
                if (!m.Success)
                    continue;

                var originalFragment = m.Value;
                currentText = pattern.Replace(currentText, match.ExpectedTranslation);

                corrections.Add(new GlossaryCorrection(
                    match.EnglishTerm,
                    match.ExpectedTranslation,
                    originalFragment));

                logger.LogInformation(
                    "Glossary hard-block: replaced '{Original}' → '{Replacement}' (term: '{Term}')",
                    originalFragment, match.ExpectedTranslation, match.EnglishTerm);

                break; // First matching variant wins; move to next term
            }
        }

        return new GlossaryReplacementResult(
            currentText,
            corrections.AsReadOnly(),
            corrections.Count > 0);
    }

    /// <summary>
    /// Builds a case-insensitive regex pattern for a single term.
    /// Single-word terms use word boundaries; multi-word terms match literally.
    /// </summary>
    private static Regex BuildPattern(string term)
    {
        var escaped = Regex.Escape(term);
        var isMultiWord = term.Contains(' ') || term.Contains('-');

        var pattern = isMultiWord
            ? escaped
            : @"\b" + escaped + @"\b";

        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>
    /// Yields the original term plus near-miss variants (hyphen removed, hyphens → spaces).
    /// This handles cases like "Personal-Protective-Equipment" appearing in translated text
    /// when the glossary term is "Personal Protective Equipment".
    /// </summary>
    private static IEnumerable<string> BuildVariants(string term)
    {
        yield return term;

        if (term.Contains('-'))
        {
            yield return term.Replace("-", " "); // hyphens → spaces
            yield return term.Replace("-", "");  // hyphens removed entirely
        }

        if (term.Contains(' '))
        {
            yield return term.Replace(" ", "-"); // spaces → hyphens
        }
    }
}
