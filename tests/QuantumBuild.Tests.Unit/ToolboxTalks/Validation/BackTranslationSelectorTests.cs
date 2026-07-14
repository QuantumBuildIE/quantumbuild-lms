using FluentAssertions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;
using Xunit;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Validation;

public class BackTranslationSelectorTests
{
    private readonly BackTranslationSelector _sut = new();

    [Fact]
    public void Select_OnlyAPresent_ReturnsA()
    {
        var result = _sut.Select(new TranslationValidationResult
        {
            BackTranslationA = "text A", ScoreA = 80
        });

        result.Text.Should().Be("text A");
        result.Score.Should().Be(80);
        result.ProviderLabel.Should().Be("Claude Haiku");
    }

    [Fact]
    public void Select_ABPresent_AHigherScore_ReturnsA()
    {
        var result = _sut.Select(new TranslationValidationResult
        {
            BackTranslationA = "text A", ScoreA = 85,
            BackTranslationB = "text B", ScoreB = 70
        });

        result.Text.Should().Be("text A");
        result.ProviderLabel.Should().Be("Claude Haiku");
    }

    [Fact]
    public void Select_ABPresent_BHigherScore_ReturnsB()
    {
        var result = _sut.Select(new TranslationValidationResult
        {
            BackTranslationA = "text A", ScoreA = 70,
            BackTranslationB = "text B", ScoreB = 90
        });

        result.Text.Should().Be("text B");
        result.Score.Should().Be(90);
        result.ProviderLabel.Should().Be("DeepL");
    }

    [Fact]
    public void Select_ABCPresent_CHighestScore_ReturnsC()
    {
        var result = _sut.Select(new TranslationValidationResult
        {
            BackTranslationA = "text A", ScoreA = 70,
            BackTranslationB = "text B", ScoreB = 75,
            BackTranslationC = "text C", ScoreC = 90
        });

        result.Text.Should().Be("text C");
        result.ProviderLabel.Should().Be("Gemini");
    }

    [Fact]
    public void Select_AllFourPresent_DHighestScore_ReturnsD()
    {
        var result = _sut.Select(new TranslationValidationResult
        {
            BackTranslationA = "text A", ScoreA = 70,
            BackTranslationB = "text B", ScoreB = 75,
            BackTranslationC = "text C", ScoreC = 80,
            BackTranslationD = "text D", ScoreD = 95
        });

        result.Text.Should().Be("text D");
        result.Score.Should().Be(95);
        result.ProviderLabel.Should().Be("Claude Sonnet");
    }

    [Fact]
    public void Select_TiedScores_ProviderOrderTiebreak_ReturnsA()
    {
        // A and B have equal scores → A wins (lower provider index)
        var result = _sut.Select(new TranslationValidationResult
        {
            BackTranslationA = "text A", ScoreA = 80,
            BackTranslationB = "text B", ScoreB = 80
        });

        result.Text.Should().Be("text A");
        result.ProviderLabel.Should().Be("Claude Haiku");
    }

    [Fact]
    public void Select_AllBackTranslationsNull_ThrowsInvalidOperationException()
    {
        var act = () => _sut.Select(new TranslationValidationResult());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No back-translations are available*");
    }
}
