namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Records a single glossary term that was auto-corrected in a translated section.
/// </summary>
public record GlossaryCorrection(
    string EnglishTerm,
    string AppliedTranslation,
    string OriginalFragment);

/// <summary>
/// Result of applying glossary hard-block replacements to a translated text.
/// </summary>
public record GlossaryReplacementResult(
    string CorrectedText,
    IReadOnlyList<GlossaryCorrection> Corrections,
    bool WasModified);

/// <summary>
/// Applies mandatory glossary term replacements to translated text before scoring.
/// When the English source term appears verbatim in translated output, replaces it
/// with the approved translation so the corrected text is what gets back-translated
/// and scored — not the flawed original.
/// </summary>
public interface IGlossaryReplacementService
{
    GlossaryReplacementResult Apply(string text, IEnumerable<GlossaryMatch> matches);
}
