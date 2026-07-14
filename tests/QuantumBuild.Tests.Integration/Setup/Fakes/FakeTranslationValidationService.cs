using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.Setup.Fakes;

/// <summary>
/// Fake ITranslationValidationService for integration tests.
///
/// Simulated:
/// - Upsert by {ValidationRunId, SectionIndex} — same lookup as the real service
/// - Reviewer-decision preservation: ReviewerDecision / DecisionAt / DecisionBy /
///   EditedTranslation are only set on NEW entities (Id == Guid.Empty). Existing
///   user decisions (Edited, Accepted, Rejected) are never overwritten.
/// - Deterministic Pass result: Outcome = Pass, FinalScore = 95, ScoreA = 95, ScoreB = 95
///
/// Not simulated:
/// - Real translation API calls (Claude Haiku, DeepL, Gemini)
/// - Glossary replacement / correction logic
/// - Safety-critical threshold bumping
/// - Multi-round consensus iteration
/// - Artefact scan / registry violations / word diff
/// </summary>
public class FakeTranslationValidationService(IToolboxTalksDbContext dbContext)
    : ITranslationValidationService
{
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
            entity = new TranslationValidationResult
            {
                ValidationRunId = validationRunId,
                SectionIndex = sectionIndex
            };
        }

        entity.SectionTitle = sectionTitle;
        entity.OriginalText = originalText;
        entity.TranslatedText = translatedText;
        entity.FinalScore = 95;
        entity.ScoreA = 95;
        entity.ScoreB = 95;
        entity.RoundsUsed = 1;
        entity.Outcome = ValidationOutcome.Pass;
        entity.EngineOutcome = ValidationOutcome.Pass;
        entity.EffectiveThreshold = passThreshold;

        // Only set reviewer decision fields on new entities (Id == Guid.Empty).
        // Existing user decisions (Edited, Accepted, Rejected) must not be overwritten.
        // This mirrors the real TranslationValidationService guard at lines 275-279.
        if (entity.Id == Guid.Empty)
        {
            entity.ReviewerDecision = ReviewerDecision.Pending;
            entity.EditedTranslation = null;
        }

        if (persist)
            await dbContext.SaveChangesAsync(cancellationToken);

        return entity;
    }
}
