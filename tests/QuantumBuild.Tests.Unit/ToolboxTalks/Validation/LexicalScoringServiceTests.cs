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
}
