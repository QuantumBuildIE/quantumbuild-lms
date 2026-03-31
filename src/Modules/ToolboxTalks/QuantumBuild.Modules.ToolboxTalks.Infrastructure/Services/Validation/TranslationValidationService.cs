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
        Guid? toolboxTalkId = null)
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

        // 2. Consensus engine — back-translate and score
        var consensus = await consensusEngine.RunAsync(
            originalText, translatedText,
            sourceLanguage, targetLanguage,
            effectiveThreshold, cancellationToken,
            tenantId: tenantId, toolboxTalkId: toolboxTalkId);

        // 3. Glossary term verification
        var engineOutcome = consensus.Outcome;
        GlossaryVerificationResult? glossaryResult = null;
        if (safetyResult.GlossaryMatches.Count > 0)
        {
            glossaryResult = glossaryVerifier.Verify(
                translatedText, safetyResult.GlossaryMatches, targetLanguage);

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

        // 4. Artefact scan — detect translation anomalies
        var artefactResult = artefactScanner.Scan(originalText, translatedText, targetLanguage);
        if (artefactResult.HasArtefacts && consensus.Outcome == Domain.Enums.ValidationOutcome.Pass)
        {
            consensus.Outcome = Domain.Enums.ValidationOutcome.Review;
            logger.LogInformation(
                "Section '{Title}' downgraded from Pass to Review due to {Count} artefact(s)",
                sectionTitle, artefactResult.Artefacts.Count);
        }

        // 5. Safety term registry scan — obligation language verification
        var registryResult = registryService.Scan(translatedText, targetLanguage);
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

        // 6. Build ReviewReasonsJson — collect all reasons for Review or Fail
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

        // 7. Upsert the result entity — find existing row for {RunId, SectionIndex} or create new
        var entity = await dbContext.TranslationValidationResults
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.ValidationRunId == validationRunId
                && r.SectionIndex == sectionIndex, cancellationToken);

        if (entity == null)
        {
            entity = new TranslationValidationResult
            {
                ValidationRunId = validationRunId,
                SectionIndex = sectionIndex
            };
            dbContext.TranslationValidationResults.Add(entity);
        }

        entity.SectionTitle = sectionTitle;
        entity.OriginalText = originalText;
        entity.TranslatedText = translatedText;
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
        // Reset reviewer decision on re-validation
        entity.ReviewerDecision = Domain.Enums.ReviewerDecision.Pending;
        entity.EditedTranslation = null;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Section '{Title}' validated. Outcome={Outcome}, FinalScore={Score}, " +
            "SafetyCritical={Safety}, Rounds={Rounds}, Artefacts={ArtefactCount}, " +
            "RegistryViolations={ViolationCount}, ResultId={Id}",
            sectionTitle, entity.Outcome, entity.FinalScore,
            entity.IsSafetyCritical, entity.RoundsUsed,
            artefactResult.Artefacts.Count, registryResult.Violations.Count,
            entity.Id);

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
