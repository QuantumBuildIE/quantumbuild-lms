namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

public record GlossaryVerificationResult(List<GlossaryMismatch> Mismatches)
{
    public bool HasMismatches => Mismatches.Count > 0;
}

public record GlossaryMismatch(
    string Term,
    string ExpectedTranslation,
    string? ActualFound);

public interface IGlossaryTermVerificationService
{
    GlossaryVerificationResult Verify(
        string translatedText,
        List<GlossaryMatch> glossaryMatches,
        string targetLanguageCode);
}
