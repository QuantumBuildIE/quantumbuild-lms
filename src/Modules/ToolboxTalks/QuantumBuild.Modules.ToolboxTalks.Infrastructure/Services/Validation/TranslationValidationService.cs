using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ArtefactScan;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.SafetyTermRegistry;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

/// <summary>
/// Orchestrates validation of a single translated section:
/// 1. Runs consensus engine for back-translations and scoring
/// 2. Runs safety classification to detect critical content
/// 3. Runs glossary term verification
/// 4. Runs artefact scan to detect translation anomalies
/// 5. Runs safety term registry scan for obligation language
/// 6. Builds ReviewReasonsJson for Review/Fail outcomes
/// 7. Persists a TranslationValidationResult to the database
/// </summary>
public class TranslationValidationService(
    IConsensusEngine consensusEngine,
    ISafetyClassificationService safetyClassifier,
    IGlossaryTermVerificationService glossaryVerifier,
    IGlossaryReplacementService glossaryReplacer,
    IWordDiffService wordDiff,
    IArtefactScanService artefactScanner,
    ISafetyTermRegistryService registryService,
    IToolboxTalksDbContext dbContext,
    IOptions<TranslationValidationSettings> settings,
    ILogger<TranslationValidationService> logger)
    : ITranslationValidationService
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public async Task<TranslationValidationResult> ValidateSectionAsync(
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
        bool persist = true)
    {
        logger.LogInformation(
            "Validating section {Index} '{Title}' for run {RunId}. " +
            "OriginalLength={OrigLen}, TranslatedLength={TransLen}",
            sectionIndex, sectionTitle, validationRunId,
            originalText.Length, translatedText.Length);

        // 1. Safety classification — detect critical content and apply threshold bump
        var safetyResult = await safetyClassifier.ClassifyAsync(
            originalText, sectorKey ?? "general", targetLanguage, cancellationToken);

        var effectiveThreshold = passThreshold;
        if (safetyResult.IsSafetyCritical)
        {
            effectiveThreshold += settings.Value.SafetyCriticalBump;
            logger.LogInformation(
                "Section '{Title}' is safety-critical. Threshold bumped from {Base} to {Effective} (+{Bump})",
                sectionTitle, passThreshold, effectiveThreshold, settings.Value.SafetyCriticalBump);
        }

        // 2. Glossary hard-block — replace English source terms with approved translations
        //    BEFORE consensus scoring so the corrected text is what gets back-translated and scored.
        var replacementResult = glossaryReplacer.Apply(translatedText, safetyResult.GlossaryMatches);
        var effectiveTranslatedText = replacementResult.WasModified
            ? replacementResult.CorrectedText
            : translatedText;
        if (replacementResult.WasModified)
        {
            logger.LogInformation(
                "Section '{Title}': {Count} glossary term(s) auto-corrected before consensus scoring",
                sectionTitle, replacementResult.Corrections.Count);
        }

        // 3. Consensus engine — back-translate and score (uses corrected text if replacements applied)
        var consensus = await consensusEngine.RunAsync(
            originalText, effectiveTranslatedText,
            sourceLanguage, targetLanguage,
            effectiveThreshold, cancellationToken,
            tenantId: tenantId, toolboxTalkId: toolboxTalkId);

        // 4. Glossary term verification (runs on corrected text — corrections resolve mismatches)
        var engineOutcome = consensus.Outcome;
        GlossaryVerificationResult? glossaryResult = null;
        if (safetyResult.GlossaryMatches.Count > 0)
        {
            glossaryResult = glossaryVerifier.Verify(
                effectiveTranslatedText, safetyResult.GlossaryMatches, targetLanguage);

            if (glossaryResult.HasMismatches)
            {
                logger.LogWarning(
                    "Section '{Title}' has {Count} glossary mismatch(es)",
                    sectionTitle, glossaryResult.Mismatches.Count);

                // Glossary mismatches force Review at minimum
                if (consensus.Outcome == Domain.Enums.ValidationOutcome.Pass)
                {
                    consensus.Outcome = Domain.Enums.ValidationOutcome.Review;
                    logger.LogInformation(
                        "Section '{Title}' downgraded from Pass to Review due to glossary mismatches",
                        sectionTitle);
                }
            }
        }

        // 5. Artefact scan — detect translation anomalies in the corrected text
        var artefactResult = artefactScanner.Scan(originalText, effectiveTranslatedText, targetLanguage);
        if (artefactResult.HasArtefacts && consensus.Outcome == Domain.Enums.ValidationOutcome.Pass)
        {
            consensus.Outcome = Domain.Enums.ValidationOutcome.Review;
            logger.LogInformation(
                "Section '{Title}' downgraded from Pass to Review due to {Count} artefact(s)",
                sectionTitle, artefactResult.Artefacts.Count);
        }

        // 6. Safety term registry scan — obligation language verification
        var registryResult = registryService.Scan(effectiveTranslatedText, targetLanguage);
        if (registryResult.HasViolations)
        {
            if (consensus.Outcome != Domain.Enums.ValidationOutcome.Fail)
            {
                consensus.Outcome = Domain.Enums.ValidationOutcome.Review;
            }
            logger.LogWarning(
                "Section '{Title}' forced to Review due to {Count} registry violation(s)",
                sectionTitle, registryResult.Violations.Count);
        }

        // 7. Build ReviewReasonsJson — collect all reasons for Review or Fail
        string? reviewReasonsJson = null;
        if (consensus.Outcome is Domain.Enums.ValidationOutcome.Review
            or Domain.Enums.ValidationOutcome.Fail)
        {
            var reasons = new List<ReviewReason>();

            // Low score
            if (consensus.FinalScore < effectiveThreshold)
            {
                reasons.Add(new ReviewReason(
                    "LowScore",
                    $"Score {consensus.FinalScore}% is below the {effectiveThreshold}% threshold",
                    $"Rounds used: {consensus.RoundsUsed}"));
            }

            // Safety-critical bump
            if (safetyResult.IsSafetyCritical)
            {
                var criticalTermsCsv = string.Join(", ", safetyResult.CriticalTermsFound);
                reasons.Add(new ReviewReason(
                    "SafetyCriticalBump",
                    $"Safety-critical content — threshold raised to {effectiveThreshold}%",
                    $"Critical terms: {criticalTermsCsv}"));
            }

            // Glossary mismatches
            if (glossaryResult?.HasMismatches == true)
            {
                foreach (var m in glossaryResult.Mismatches)
                {
                    reasons.Add(new ReviewReason(
                        "GlossaryMismatch",
                        $"Expected translation for '{m.Term}' not found",
                        $"Expected '{m.ExpectedTranslation}' in {targetLanguage}"));
                }
            }

            // Artefacts
            if (artefactResult.HasArtefacts)
            {
                foreach (var a in artefactResult.Artefacts)
                {
                    reasons.Add(new ReviewReason(
                        "ArtefactDetected",
                        ArtefactMessage(a.Type),
                        a.Detail));
                }
            }

            // Registry violations
            if (registryResult.HasViolations)
            {
                foreach (var v in registryResult.Violations)
                {
                    reasons.Add(new ReviewReason(
                        "RegistryViolation",
                        "Obligation language may be weakened",
                        $"'{v.FoundBadPattern}' found — use '{v.RequiredTerm}' instead. {v.Reason}"));
                }
            }

            if (reasons.Count > 0)
            {
                reviewReasonsJson = JsonSerializer.Serialize(reasons, CamelCase);
            }
        }

        // 8. Build result entity — upsert if persist=true, return in-memory if persist=false (corpus dry-run)
        TranslationValidationResult entity;

        if (persist)
        {
            entity = await dbContext.TranslationValidationResults
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.ValidationRunId == validationRunId
                    && r.SectionIndex == sectionIndex, cancellationToken)
                ?? new TranslationValidationResult
                {
                    ValidationRunId = validationRunId,
                    SectionIndex = sectionIndex
                };

            if (entity.Id == Guid.Empty)
                dbContext.TranslationValidationResults.Add(entity);
        }
        else
        {
            // Dry-run: construct in-memory without touching the DB
            entity = new TranslationValidationResult
            {
                ValidationRunId = validationRunId,
                SectionIndex = sectionIndex
            };
        }

        entity.SectionTitle = sectionTitle;
        entity.OriginalText = originalText;
        entity.TranslatedText = effectiveTranslatedText;
        entity.BackTranslationA = consensus.BackTranslationA;
        entity.BackTranslationB = consensus.BackTranslationB;
        entity.BackTranslationC = consensus.BackTranslationC;
        entity.BackTranslationD = consensus.BackTranslationD;
        entity.ScoreA = consensus.ScoreA;
        entity.ScoreB = consensus.ScoreB;
        entity.ScoreC = consensus.ScoreC;
        entity.ScoreD = consensus.ScoreD;
        entity.FinalScore = consensus.FinalScore;
        entity.RoundsUsed = consensus.RoundsUsed;
        entity.Outcome = consensus.Outcome;
        entity.EngineOutcome = engineOutcome;
        entity.IsSafetyCritical = safetyResult.IsSafetyCritical;
        entity.CriticalTerms = safetyResult.CriticalTermsFound.Count > 0
            ? JsonSerializer.Serialize(safetyResult.CriticalTermsFound)
            : null;
        entity.GlossaryMismatches = glossaryResult?.HasMismatches == true
            ? JsonSerializer.Serialize(glossaryResult.Mismatches)
            : null;
        entity.EffectiveThreshold = effectiveThreshold;
        entity.ArtefactsJson = artefactResult.HasArtefacts
            ? JsonSerializer.Serialize(artefactResult.Artefacts, CamelCase)
            : null;
        entity.RegistryViolationsJson = registryResult.HasViolations
            ? JsonSerializer.Serialize(registryResult.Violations, CamelCase)
            : null;
        entity.ReviewReasonsJson = reviewReasonsJson;
        entity.GlossaryCorrectionsJson = replacementResult.WasModified
            ? JsonSerializer.Serialize(replacementResult.Corrections, CamelCase)
            : null;
        entity.GlossaryHardBlockApplied = replacementResult.WasModified ? true : null;
        // Reset reviewer decision on re-validation
        entity.ReviewerDecision = Domain.Enums.ReviewerDecision.Pending;
        entity.EditedTranslation = null;

        if (persist)
            await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Section '{Title}' validated (persist={Persist}). Outcome={Outcome}, FinalScore={Score}, " +
            "SafetyCritical={Safety}, Rounds={Rounds}, Artefacts={ArtefactCount}, " +
            "RegistryViolations={ViolationCount}, GlossaryCorrections={CorrectionCount}",
            sectionTitle, persist, entity.Outcome, entity.FinalScore,
            entity.IsSafetyCritical, entity.RoundsUsed,
            artefactResult.Artefacts.Count, registryResult.Violations.Count,
            replacementResult.Corrections.Count);

        return entity;
    }

    private static string ArtefactMessage(ArtefactType type) => type switch
    {
        ArtefactType.UntranslatedEnglish => "Possible untranslated English detected",
        ArtefactType.PossibleTruncation => "Translation may be truncated",
        ArtefactType.DuplicatedPhrase => "Duplicated phrase detected",
        ArtefactType.CollapsedBulletList => "Bullet list may have collapsed",
        ArtefactType.StrayNumber => "Stray number detected mid-sentence",
        _ => $"Artefact detected: {type}"
    };

    private record ReviewReason(string Type, string Message, string Detail);
}
