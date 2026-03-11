using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

        // 4. Upsert the result entity — find existing row for {RunId, SectionIndex} or create new
        var entity = await _dbContext.TranslationValidationResults
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
            _dbContext.TranslationValidationResults.Add(entity);
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
        // Reset reviewer decision on re-validation
        entity.ReviewerDecision = Domain.Enums.ReviewerDecision.Pending;
        entity.EditedTranslation = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Section '{Title}' validated. Outcome={Outcome}, FinalScore={Score}, " +
            "SafetyCritical={Safety}, Rounds={Rounds}, ResultId={Id}",
            sectionTitle, entity.Outcome, entity.FinalScore,
            entity.IsSafetyCritical, entity.RoundsUsed, entity.Id);

        return entity;
    }
}
