using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
/// 4. Runs word diff for the diff result
/// 5. Persists a TranslationValidationResult to the database
/// </summary>
public class TranslationValidationService : ITranslationValidationService
{
    private readonly IConsensusEngine _consensusEngine;
    private readonly ISafetyClassificationService _safetyClassifier;
    private readonly IGlossaryTermVerificationService _glossaryVerifier;
    private readonly IWordDiffService _wordDiff;
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly TranslationValidationSettings _settings;
    private readonly ILogger<TranslationValidationService> _logger;

    public TranslationValidationService(
        IConsensusEngine consensusEngine,
        ISafetyClassificationService safetyClassifier,
        IGlossaryTermVerificationService glossaryVerifier,
        IWordDiffService wordDiff,
        IToolboxTalksDbContext dbContext,
        IOptions<TranslationValidationSettings> settings,
        ILogger<TranslationValidationService> logger)
    {
        _consensusEngine = consensusEngine;
        _safetyClassifier = safetyClassifier;
        _glossaryVerifier = glossaryVerifier;
        _wordDiff = wordDiff;
        _dbContext = dbContext;
        _settings = settings.Value;
        _logger = logger;
    }

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
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Validating section {Index} '{Title}' for run {RunId}. " +
            "OriginalLength={OrigLen}, TranslatedLength={TransLen}",
            sectionIndex, sectionTitle, validationRunId,
            originalText.Length, translatedText.Length);

        // 1. Safety classification — detect critical content and apply threshold bump
        var safetyResult = await _safetyClassifier.ClassifyAsync(
            originalText, sectorKey ?? "general", targetLanguage, cancellationToken);

        var effectiveThreshold = passThreshold;
        if (safetyResult.IsSafetyCritical)
        {
            effectiveThreshold += _settings.SafetyCriticalBump;
            _logger.LogInformation(
                "Section '{Title}' is safety-critical. Threshold bumped from {Base} to {Effective} (+{Bump})",
                sectionTitle, passThreshold, effectiveThreshold, _settings.SafetyCriticalBump);
        }

        // 2. Consensus engine — back-translate and score
        var consensus = await _consensusEngine.RunAsync(
            originalText, translatedText,
            sourceLanguage, targetLanguage,
            effectiveThreshold, cancellationToken);

        // 3. Glossary term verification
        var engineOutcome = consensus.Outcome;
        GlossaryVerificationResult? glossaryResult = null;
        if (safetyResult.GlossaryMatches.Count > 0)
        {
            glossaryResult = _glossaryVerifier.Verify(
                translatedText, safetyResult.GlossaryMatches, targetLanguage);

            if (glossaryResult.HasMismatches)
            {
                _logger.LogWarning(
                    "Section '{Title}' has {Count} glossary mismatch(es)",
                    sectionTitle, glossaryResult.Mismatches.Count);

                // Glossary mismatches force Review at minimum
                if (consensus.Outcome == Domain.Enums.ValidationOutcome.Pass)
                {
                    consensus.Outcome = Domain.Enums.ValidationOutcome.Review;
                    _logger.LogInformation(
                        "Section '{Title}' downgraded from Pass to Review due to glossary mismatches",
                        sectionTitle);
                }
            }
        }

        // 4. Build and persist the result entity
        var entity = new TranslationValidationResult
        {
            ValidationRunId = validationRunId,
            SectionIndex = sectionIndex,
            SectionTitle = sectionTitle,
            OriginalText = originalText,
            TranslatedText = translatedText,
            BackTranslationA = consensus.BackTranslationA,
            BackTranslationB = consensus.BackTranslationB,
            BackTranslationC = consensus.BackTranslationC,
            BackTranslationD = consensus.BackTranslationD,
            ScoreA = consensus.ScoreA,
            ScoreB = consensus.ScoreB,
            ScoreC = consensus.ScoreC,
            ScoreD = consensus.ScoreD,
            FinalScore = consensus.FinalScore,
            RoundsUsed = consensus.RoundsUsed,
            Outcome = consensus.Outcome,
            EngineOutcome = engineOutcome,
            IsSafetyCritical = safetyResult.IsSafetyCritical,
            CriticalTerms = safetyResult.CriticalTermsFound.Count > 0
                ? JsonSerializer.Serialize(safetyResult.CriticalTermsFound)
                : null,
            GlossaryMismatches = glossaryResult?.HasMismatches == true
                ? JsonSerializer.Serialize(glossaryResult.Mismatches)
                : null,
            EffectiveThreshold = effectiveThreshold
        };

        _dbContext.TranslationValidationResults.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Section '{Title}' validated. Outcome={Outcome}, FinalScore={Score}, " +
            "SafetyCritical={Safety}, Rounds={Rounds}, ResultId={Id}",
            sectionTitle, entity.Outcome, entity.FinalScore,
            entity.IsSafetyCritical, entity.RoundsUsed, entity.Id);

        return entity;
    }
}
