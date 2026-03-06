using FluentAssertions;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;
using Xunit;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Validation;

public class LexicalScoringServiceTests
{
    private readonly LexicalScoringService _sut = new();

    [Fact]
    public void Score_IdenticalStrings_Returns100()
    {
        var score = _sut.Score(
            "Never stand behind reversing vehicles",
            "Never stand behind reversing vehicles");

        score.Should().Be(100.0);
    }

    [Fact]
    public void Score_PartialOverlap_ReturnsExpectedScore()
    {
        var score = _sut.Score(
            "Never stand behind reversing vehicles",
            "Do not stand in front of moving vehicles");

        // Original tokens: [never, stand, behind, reversing, vehicles] = 5
        // Candidate tokens: [do, not, stand, in, front, of, moving, vehicles] = 8
        // Matching: stand, vehicles = 2
        // Score: 2 / max(5, 8) * 100 = 25.0
        score.Should().Be(25.0);
    }

    [Fact]
    public void Score_BothEmpty_Returns100()
    {
        var score = _sut.Score("", "");

        score.Should().Be(100.0);
    }

    [Fact]
    public void Score_SingleMatchingToken_Returns100()
    {
        var score = _sut.Score("PPE", "PPE");

        score.Should().Be(100.0);
    }

    [Fact]
    public void Score_NoMatchingTokens_Returns0()
    {
        var score = _sut.Score("alpha beta", "gamma delta");

        score.Should().Be(0.0);
    }

    [Fact]
    public void Score_OneEmpty_OneNonEmpty_Returns0()
    {
        var score = _sut.Score("", "hello world");

        score.Should().Be(0.0);
    }

    [Fact]
    public void Score_OneNonEmpty_OneEmpty_Returns0()
    {
        var score = _sut.Score("hello world", "");

        score.Should().Be(0.0);
    }

    [Fact]
    public void Score_WhitespaceOnlyStrings_Returns100()
    {
        var score = _sut.Score("   ", "   ");

        score.Should().Be(100.0);
    }

    [Fact]
    public void Score_WhitespaceOnly_VsNonEmpty_Returns0()
    {
        var score = _sut.Score("   ", "hello");

        score.Should().Be(0.0);
    }

    [Fact]
    public void Score_CaseInsensitive_PPE_LowerCase_Matches()
    {
        var score = _sut.Score("PPE", "ppe");

        score.Should().Be(100.0);
    }

    [Fact]
    public void Score_CaseInsensitive_MixedCase_Matches()
    {
        var score = _sut.Score("Wear PPE Always", "wear ppe always");

        score.Should().Be(100.0);
    }
}
