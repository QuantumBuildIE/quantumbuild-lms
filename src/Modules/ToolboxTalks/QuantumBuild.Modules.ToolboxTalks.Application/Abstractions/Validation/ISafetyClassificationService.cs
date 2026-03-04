namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

public record SafetyClassificationResult(
    bool IsSafetyCritical,
    List<string> CriticalTermsFound,
    List<GlossaryMatch> GlossaryMatches);

public record GlossaryMatch(
    string EnglishTerm,
    string Category,
    string? ExpectedTranslation);

public interface ISafetyClassificationService
{
    Task<SafetyClassificationResult> ClassifyAsync(
        string text,
        string sectorKey,
        string targetLanguageCode,
        CancellationToken cancellationToken = default);
}
