using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Validation;

public class ConsensusEngineTests
{
    private const string OriginalText = "Always wear PPE on site";
    private const string TranslatedText = "Zawsze noś ŚOI na budowie";
    private const string SourceLang = "en";
    private const string TargetLang = "pl";
    private const int Threshold = 75;

    private readonly Mock<IClaudeHaikuBackTranslationService> _claudeHaiku = new();
    private readonly Mock<IDeepLTranslationService> _deepL = new();
    private readonly Mock<IGeminiTranslationService> _gemini = new();
    private readonly Mock<IDeepSeekTranslationService> _deepSeek = new();
    private readonly Mock<ILexicalScoringService> _scorer = new();

    private ConsensusEngine CreateEngine(int maxRounds = 3)
    {
        var settings = Options.Create(new TranslationValidationSettings { MaxRounds = maxRounds });
        var logger = Mock.Of<ILogger<ConsensusEngine>>();
        return new ConsensusEngine(
            _claudeHaiku.Object, _deepL.Object, _gemini.Object,
            _deepSeek.Object, _scorer.Object, settings, logger);
    }

    private static BackTranslationResult SuccessBt(string text, string provider) =>
        BackTranslationResult.SuccessResult(text, provider);

    private void SetupScorer(string backTranslated, double score)
    {
        _scorer.Setup(s => s.Score(OriginalText, backTranslated)).Returns(score);
    }

    // ── Round 1 passes: both A and B above threshold, high agreement ──

    [Fact]
    public async Task RunAsync_Round1Pass_BothAboveThreshold_HighAgreement()
    {
        var btA = "Always wear PPE on site";
        var btB = "Always wear PPE on the site";
        _claudeHaiku.Setup(c => c.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<Guid?>()))
            .ReturnsAsync(SuccessBt(btA, "Claude"));
        _deepL.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessBt(btB, "DeepL"));
        SetupScorer(btA, 90.0);
        SetupScorer(btB, 85.0);

        var result = await CreateEngine().RunAsync(OriginalText, TranslatedText, SourceLang, TargetLang, Threshold);

        result.Outcome.Should().Be(ValidationOutcome.Pass);
        result.RoundsUsed.Should().Be(1);
        result.ScoreA.Should().Be(90);
        result.ScoreB.Should().Be(85);
        result.FinalScore.Should().Be(88); // avg(90, 85) = 87.5 → 88
    }

    // ── Round 1 inconclusive — scores diverge → escalates to Round 2 ──

    [Fact]
    public async Task RunAsync_Round1Inconclusive_ScoresDiverge_EscalatesToRound2()
    {
        var btA = "Always wear PPE on site";
        var btB = "Put on protective equipment";
        var btC = "Always wear protective equipment on site";
        _claudeHaiku.Setup(c => c.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<Guid?>()))
            .ReturnsAsync(SuccessBt(btA, "Claude"));
        _deepL.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessBt(btB, "DeepL"));
        _gemini.Setup(g => g.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessBt(btC, "Gemini"));
        SetupScorer(btA, 90.0);
        SetupScorer(btB, 60.0); // diverges from A by 30 points
        SetupScorer(btC, 85.0);

        var result = await CreateEngine().RunAsync(OriginalText, TranslatedText, SourceLang, TargetLang, Threshold);

        // avg(90, 60, 85) = 78.33 → 78 ≥ 75 → Pass via Round 2
        result.Outcome.Should().Be(ValidationOutcome.Pass);
        result.RoundsUsed.Should().Be(2);
        result.ScoreC.Should().Be(85);
    }

    // ── Round 2 resolves ──

    [Fact]
    public async Task RunAsync_Round2Resolves_OutcomeDetermined()
    {
        var btA = "Wear PPE always";
        var btB = "Use protective gear";
        var btC = "Always use PPE on site";
        _claudeHaiku.Setup(c => c.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<Guid?>()))
            .ReturnsAsync(SuccessBt(btA, "Claude"));
        _deepL.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessBt(btB, "DeepL"));
        _gemini.Setup(g => g.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessBt(btC, "Gemini"));
        SetupScorer(btA, 80.0);
        SetupScorer(btB, 50.0); // diverges, R1 fails
        SetupScorer(btC, 82.0); // avg(80, 50, 82) = 70.67 → 71 < 75 → goes to R3

        // DeepSeek returns null → still resolves at R3 with available scores
        _deepSeek.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);

        var result = await CreateEngine().RunAsync(OriginalText, TranslatedText, SourceLang, TargetLang, Threshold);

        result.RoundsUsed.Should().Be(3);
        // avg(80, 50, 82) = 70.67 → 71 — within 15 of 75 → Review
        result.Outcome.Should().Be(ValidationOutcome.Review);
    }

    // ── All rounds inconclusive → Review ──

    [Fact]
    public async Task RunAsync_AllRoundsInconclusive_OutcomeReview()
    {
        var btA = "Wear PPE";
        var btB = "Use gear";
        _claudeHaiku.Setup(c => c.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<Guid?>()))
            .ReturnsAsync(SuccessBt(btA, "Claude"));
        _deepL.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessBt(btB, "DeepL"));
        _gemini.Setup(g => g.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null); // unavailable
        _deepSeek.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null); // unavailable
        SetupScorer(btA, 70.0);
        SetupScorer(btB, 65.0);

        var result = await CreateEngine().RunAsync(OriginalText, TranslatedText, SourceLang, TargetLang, Threshold);

        result.RoundsUsed.Should().Be(3);
        // avg(70, 65) = 67.5 → 68, within 15 of 75 → Review
        result.Outcome.Should().Be(ValidationOutcome.Review);
    }

    // ── Provider A returns null → skips gracefully ──

    [Fact]
    public async Task RunAsync_ProviderAReturnsNull_SkipsGracefully()
    {
        var btB = "Always wear PPE on site";
        _claudeHaiku.Setup(c => c.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<Guid?>()))
            .ReturnsAsync((BackTranslationResult?)null);
        _deepL.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessBt(btB, "DeepL"));
        _gemini.Setup(g => g.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);
        _deepSeek.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);
        SetupScorer(btB, 90.0);

        var result = await CreateEngine().RunAsync(OriginalText, TranslatedText, SourceLang, TargetLang, Threshold);

        // R1 fails (A is null), R2 skipped (Gemini null), R3 (DeepSeek null)
        // Final: avg(90) = 90 ≥ 75 → Pass
        result.Outcome.Should().Be(ValidationOutcome.Pass);
        result.BackTranslationA.Should().BeNull();
        result.ScoreB.Should().Be(90);
    }

    // ── Provider B returns null → skips gracefully ──

    [Fact]
    public async Task RunAsync_ProviderBReturnsNull_SkipsGracefully()
    {
        var btA = "Always wear PPE on site";
        _claudeHaiku.Setup(c => c.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<Guid?>()))
            .ReturnsAsync(SuccessBt(btA, "Claude"));
        _deepL.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);
        _gemini.Setup(g => g.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);
        _deepSeek.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);
        SetupScorer(btA, 85.0);

        var result = await CreateEngine().RunAsync(OriginalText, TranslatedText, SourceLang, TargetLang, Threshold);

        result.Outcome.Should().Be(ValidationOutcome.Pass);
        result.BackTranslationB.Should().BeNull();
        result.ScoreA.Should().Be(85);
    }

    // ── All providers return null → Review with zero scores ──

    [Fact]
    public async Task RunAsync_AllProvidersReturnNull_ReviewOutcomeWithZeroScores()
    {
        _claudeHaiku.Setup(c => c.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<Guid?>()))
            .ReturnsAsync((BackTranslationResult?)null);
        _deepL.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);
        _gemini.Setup(g => g.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);
        _deepSeek.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);

        var result = await CreateEngine().RunAsync(OriginalText, TranslatedText, SourceLang, TargetLang, Threshold);

        result.Outcome.Should().Be(ValidationOutcome.Fail); // 0 < 75-15 = 60 → Fail
        result.FinalScore.Should().Be(0);
        result.RoundsUsed.Should().Be(3);
    }

    // ── Score at exactly threshold → Pass (boundary) ──

    [Fact]
    public async Task RunAsync_ScoreAtExactlyThreshold_Pass()
    {
        var btA = "Wear PPE on site";
        var btB = "Wear PPE on site";
        _claudeHaiku.Setup(c => c.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<Guid?>()))
            .ReturnsAsync(SuccessBt(btA, "Claude"));
        _deepL.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessBt(btB, "DeepL"));
        SetupScorer(btA, 75.0); // exactly at threshold
        SetupScorer(btB, 75.0); // exactly at threshold, agreement = 0

        var result = await CreateEngine().RunAsync(OriginalText, TranslatedText, SourceLang, TargetLang, Threshold);

        result.Outcome.Should().Be(ValidationOutcome.Pass);
        result.RoundsUsed.Should().Be(1);
        result.FinalScore.Should().Be(75);
    }

    // ── Score one below threshold → Review (boundary) ──

    [Fact]
    public async Task RunAsync_ScoreOneBelowThreshold_Review()
    {
        var btA = "Wear PPE on site";
        var btB = "Wear PPE on site";
        _claudeHaiku.Setup(c => c.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<Guid?>()))
            .ReturnsAsync(SuccessBt(btA, "Claude"));
        _deepL.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessBt(btB, "DeepL"));
        _gemini.Setup(g => g.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);
        _deepSeek.Setup(d => d.BackTranslateAsync(TranslatedText, SourceLang, TargetLang, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BackTranslationResult?)null);
        SetupScorer(btA, 74.0);
        SetupScorer(btB, 74.0);

        var result = await CreateEngine().RunAsync(OriginalText, TranslatedText, SourceLang, TargetLang, Threshold);

        // R1 fails (74 < 75), R2 skipped, R3 skipped, final avg = 74, within 15 → Review
        result.Outcome.Should().Be(ValidationOutcome.Review);
        result.FinalScore.Should().Be(74);
    }
}
