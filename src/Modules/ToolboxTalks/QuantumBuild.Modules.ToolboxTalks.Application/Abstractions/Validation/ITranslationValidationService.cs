using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;

/// <summary>
/// Orchestrates validation of a single translated section.
/// Coordinates consensus engine, safety classification, glossary verification,
/// and word diff, then persists the result.
/// </summary>
public interface ITranslationValidationService
{
    /// <summary>
    /// Validates a single section's translation and persists the result.
    /// </summary>
    /// <param name="validationRunId">The parent validation run ID</param>
    /// <param name="sectionIndex">Zero-based index of the section within the talk</param>
    /// <param name="sectionTitle">Display title of the section</param>
    /// <param name="originalText">The original source-language text</param>
    /// <param name="translatedText">The translated text to validate</param>
    /// <param name="sourceLanguage">Source language code (e.g., "en")</param>
    /// <param name="targetLanguage">Target language code (e.g., "pl")</param>
    /// <param name="sectorKey">Industry sector key for glossary lookup (optional)</param>
    /// <param name="passThreshold">Base pass threshold (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The persisted TranslationValidationResult entity</returns>
    Task<TranslationValidationResult> ValidateSectionAsync(
        Guid validationRunId,
        int sectionIndex,
        string sectionTitle,
        string originalText,
        string translatedText,
        string sourceLanguage,
        string targetLanguage,
        string? sectorKey,
        int passThreshold,
        CancellationToken cancellationToken = default,
        Guid tenantId = default,
        Guid? toolboxTalkId = null);
}
