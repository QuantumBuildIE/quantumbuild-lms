using FluentAssertions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;
using Xunit;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Validation;

public class WordToSentenceMapperTests
{
    private readonly WordToSentenceMapper _sut = new();

    // "hello world foo" — words: "hello"(0), "world"(6), "foo"(12)
    private const string SingleSentenceText = "hello world foo";
    private static readonly SentenceSpan[] SingleSentence = [new SentenceSpan(0, 15)];

    // "alpha. beta gamma. delta." — words: "alpha."(0), "beta"(7), "gamma."(12), "delta."(19)
    // Sentences: (0,6), (7,18), (19,25)
    private const string ThreeSentenceText = "alpha. beta gamma. delta.";
    private static readonly SentenceSpan[] ThreeSentences =
    [
        new SentenceSpan(0, 6),
        new SentenceSpan(7, 18),
        new SentenceSpan(19, 25)
    ];

    [Fact]
    public void Map_SingleSentence_DiffRunInIt_ReturnsOneResult()
    {
        // word 1 = "world" at char 6 — in the single sentence (0,15)
        var result = _sut.Map(
            SingleSentenceText,
            SingleSentence,
            [new DiffRun(1, 1, DiffType.Delete)]);

        result.Should().HaveCount(1);
        result[0].StartOffset.Should().Be(0);
        result[0].EndOffset.Should().Be(15);
    }

    [Fact]
    public void Map_ThreeSentences_DiffRunInSecond_ReturnsSecondSentence()
    {
        // word 1 = "beta" at char 7 — in sentence (7,18)
        var result = _sut.Map(
            ThreeSentenceText,
            ThreeSentences,
            [new DiffRun(1, 2, DiffType.Delete)]);

        result.Should().HaveCount(1);
        result[0].StartOffset.Should().Be(7);
        result[0].EndOffset.Should().Be(18);
    }

    [Fact]
    public void Map_TwoDiffRunsInSameSentence_ReturnsOneDeduplicatedResult()
    {
        // Both words 0 ("hello") and 1 ("world") are in the single sentence
        var result = _sut.Map(
            SingleSentenceText,
            SingleSentence,
            [new DiffRun(0, 0, DiffType.Delete), new DiffRun(1, 1, DiffType.Delete)]);

        result.Should().HaveCount(1);
        result[0].StartOffset.Should().Be(0);
        result[0].EndOffset.Should().Be(15);
    }

    [Fact]
    public void Map_TwoDiffRunsInDifferentSentences_ReturnsTwoResults()
    {
        // word 0 ("alpha.") → sentence (0,6); word 3 ("delta.") → sentence (19,25)
        var result = _sut.Map(
            ThreeSentenceText,
            ThreeSentences,
            [new DiffRun(0, 0, DiffType.Delete), new DiffRun(3, 3, DiffType.Delete)]);

        result.Should().HaveCount(2);
        result[0].StartOffset.Should().Be(0);
        result[0].EndOffset.Should().Be(6);
        result[1].StartOffset.Should().Be(19);
        result[1].EndOffset.Should().Be(25);
    }

    [Fact]
    public void Map_DiffRunAtStartOfText_MapsToFirstSentence()
    {
        // word 0 at char 0 → first sentence (0,6)
        var result = _sut.Map(
            ThreeSentenceText,
            ThreeSentences,
            [new DiffRun(0, 0, DiffType.Delete)]);

        result.Should().HaveCount(1);
        result[0].StartOffset.Should().Be(0);
        result[0].EndOffset.Should().Be(6);
    }

    [Fact]
    public void Map_DiffRunAtEndOfText_MapsToLastSentence()
    {
        // word 3 ("delta.") at char 19 → last sentence (19,25)
        var result = _sut.Map(
            ThreeSentenceText,
            ThreeSentences,
            [new DiffRun(3, 3, DiffType.Delete)]);

        result.Should().HaveCount(1);
        result[0].StartOffset.Should().Be(19);
        result[0].EndOffset.Should().Be(25);
    }

    [Fact]
    public void Map_DiffRunWordIndexPastEndOfText_MapsToLastSentence()
    {
        // 4 words in ThreeSentenceText (indices 0–3); index 99 is clamped to 3 → last sentence
        var result = _sut.Map(
            ThreeSentenceText,
            ThreeSentences,
            [new DiffRun(99, 99, DiffType.Insert)]);

        result.Should().HaveCount(1);
        result[0].StartOffset.Should().Be(19);
        result[0].EndOffset.Should().Be(25);
    }

    [Fact]
    public void Map_EmptyOriginalText_ReturnsEmpty()
    {
        var result = _sut.Map(
            "",
            [new SentenceSpan(0, 0)],
            [new DiffRun(0, 0, DiffType.Delete)]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Map_SingleDiffRun_RunsCollectionContainsThatRun()
    {
        // word 1 = "world" at char 6 — single run feeding the single sentence
        var run = new DiffRun(1, 1, DiffType.Delete);
        var result = _sut.Map(
            SingleSentenceText,
            SingleSentence,
            [run]);

        result.Should().HaveCount(1);
        result[0].Runs.Should().ContainSingle().Which.Should().Be(run);
    }

    [Fact]
    public void Map_TwoDiffRunsInSameSentence_RunsCollectionContainsBoth()
    {
        // Both words 0 and 1 are in the single sentence — both runs must appear in Runs
        var run1 = new DiffRun(0, 0, DiffType.Delete);
        var run2 = new DiffRun(1, 1, DiffType.Delete);
        var result = _sut.Map(
            SingleSentenceText,
            SingleSentence,
            [run1, run2]);

        result.Should().HaveCount(1);
        result[0].Runs.Should().HaveCount(2)
            .And.Contain(run1)
            .And.Contain(run2);
    }
}
