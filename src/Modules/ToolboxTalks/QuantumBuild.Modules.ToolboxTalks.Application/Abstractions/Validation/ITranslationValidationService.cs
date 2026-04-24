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
    /// Validates a single section's translation, optionally persisting the result.
    /// </summary>
    /// <param name="validationRunId">The parent validation run ID (ignored when persist=false)</param>
    /// <param name="sectionIndex">Zero-based index of the section within the talk</param>
    /// <param name="sectionTitle">Display title of the section</param>
    /// <param name="originalText">The original source-language text</param>
    /// <param name="translatedText">The translated text to validate</param>
    /// <param name="sourceLanguage">Source language code (e.g., "en")</param>
    /// <param name="targetLanguage">Target language code (e.g., "pl")</param>
    /// <param name="sectorKey">Industry sector key for glossary lookup (optional)</param>
    /// <param name="passThreshold">Base pass threshold (0-100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="tenantId">Tenant ID for AI usage logging</param>
    /// <param name="toolboxTalkId">Reference entity ID for AI usage logging</param>
    /// <param name="persist">
    /// When true (default): upserts a TranslationValidationResult row in the database.
    /// When false (corpus dry-run): executes identical pipeline logic but returns an
    /// in-memory result without any DB writes.
    /// </param>
    /// <returns>The validation result entity (in-memory when persist=false)</returns>
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
        Guid? toolboxTalkId = null,
        bool persist = true);
}
