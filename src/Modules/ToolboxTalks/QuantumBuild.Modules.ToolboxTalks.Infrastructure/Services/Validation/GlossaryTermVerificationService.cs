using Microsoft.Extensions.Logging;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

public class GlossaryTermVerificationService(
    ILogger<GlossaryTermVerificationService> logger) : IGlossaryTermVerificationService
{
    public GlossaryVerificationResult Verify(
        string translatedText,
        List<GlossaryMatch> glossaryMatches,
        string targetLanguageCode)
    {
        var mismatches = new List<GlossaryMismatch>();

        foreach (var match in glossaryMatches)
        {
            if (string.IsNullOrWhiteSpace(match.ExpectedTranslation))
                continue; // No approved translation for this language — skip

            if (translatedText.Contains(match.ExpectedTranslation, StringComparison.OrdinalIgnoreCase))
                continue; // Found the expected translation — good

            // Not found — attempt to find what was actually used (the English term as fallback)
            string? actualFound = null;
            if (translatedText.Contains(match.EnglishTerm, StringComparison.OrdinalIgnoreCase))
                actualFound = match.EnglishTerm; // Untranslated — English term used instead

            mismatches.Add(new GlossaryMismatch(
                match.EnglishTerm,
                match.ExpectedTranslation,
                actualFound));
        }

        if (mismatches.Count > 0)
        {
            logger.LogDebug(
                "Glossary verification for language '{LanguageCode}': {MismatchCount} mismatches found",
                targetLanguageCode, mismatches.Count);
        }

        return new GlossaryVerificationResult(mismatches);
    }
}
