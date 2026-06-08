using FluentAssertions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;
using Xunit;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Validation;

public class SentenceSplitterTests
{
    private readonly SentenceSplitter _sut = new();

    [Fact]
    public void Split_SingleSentenceNoTerminator_ReturnsSingleSpanCoveringWholeString()
    {
        var result = _sut.Split("Hello world");

        result.Should().HaveCount(1);
        result[0].Should().Be(new SentenceSpan(0, 11));
    }

    [Fact]
    public void Split_SingleSentenceWithPeriod_ReturnsSingleSpan()
    {
        var result = _sut.Split("Hello world.");

        result.Should().HaveCount(1);
        result[0].Should().Be(new SentenceSpan(0, 12));
    }

    [Fact]
    public void Split_SingleSentenceWithQuestionMark_ReturnsSingleSpan()
    {
        var result = _sut.Split("Is it safe?");

        result.Should().HaveCount(1);
        result[0].Should().Be(new SentenceSpan(0, 11));
    }

    [Fact]
    public void Split_SingleSentenceWithExclamationMark_ReturnsSingleSpan()
    {
        var result = _sut.Split("Stop now!");

        result.Should().HaveCount(1);
        result[0].Should().Be(new SentenceSpan(0, 9));
    }

    [Fact]
    public void Split_TwoSentencesSeparatedByPeriod_ReturnsTwoSpans()
    {
        var result = _sut.Split("Hello world. Goodbye earth.");

        result.Should().HaveCount(2);
        result[0].Should().Be(new SentenceSpan(0, 12));
        result[1].Should().Be(new SentenceSpan(13, 27));
    }

    [Fact]
    public void Split_TwoSentencesSeparatedByQuestionMark_ReturnsTwoSpans()
    {
        var result = _sut.Split("Is it safe? Yes it is.");

        result.Should().HaveCount(2);
        result[0].Should().Be(new SentenceSpan(0, 11));
        result[1].Should().Be(new SentenceSpan(12, 22));
    }

    [Fact]
    public void Split_TwoSentencesSeparatedByExclamationMark_ReturnsTwoSpans()
    {
        var result = _sut.Split("Stop now! Be careful.");

        result.Should().HaveCount(2);
        result[0].Should().Be(new SentenceSpan(0, 9));
        result[1].Should().Be(new SentenceSpan(10, 21));
    }

    [Fact]
    public void Split_ConsecutiveExclamationMarks_SingleBoundary()
    {
        // "!!" is one terminator group → one sentence boundary
        var result = _sut.Split("Watch out!! Run now.");

        result.Should().HaveCount(2);
        result[0].Should().Be(new SentenceSpan(0, 11));
        result[1].Should().Be(new SentenceSpan(12, 20));
    }

    [Fact]
    public void Split_MixedConsecutiveTerminators_SingleBoundary()
    {
        // "!?" is one terminator group → one sentence boundary
        var result = _sut.Split("Really!? That matters.");

        result.Should().HaveCount(2);
        result[0].Should().Be(new SentenceSpan(0, 8));
        result[1].Should().Be(new SentenceSpan(9, 22));
    }

    [Fact]
    public void Split_AbbreviationFollowedByLowercase_NoSplit()
    {
        // "Dr." is in the abbreviation guard list → the "." does not split
        var result = _sut.Split("Dr. Smith examined the patient.");

        result.Should().HaveCount(1);
        result[0].Should().Be(new SentenceSpan(0, 31));
    }

    [Fact]
    public void Split_AbbreviationFollowedByUppercase_TreatedAsNonTerminator()
    {
        // Spec: abbreviations are never terminators, even before an uppercase word
        var result = _sut.Split("etc. The rest is straightforward.");

        result.Should().HaveCount(1);
        result[0].Should().Be(new SentenceSpan(0, 33));
    }

    [Fact]
    public void Split_EmptyString_ReturnsEmpty()
    {
        var result = _sut.Split("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Split_WhitespaceOnly_ReturnsEmpty()
    {
        var result = _sut.Split("   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Split_TrailingWhitespaceAfterTerminator_SpanEndsAtTerminator()
    {
        // Trailing spaces are not included in the sentence span
        var result = _sut.Split("Hello world.   ");

        result.Should().HaveCount(1);
        result[0].Should().Be(new SentenceSpan(0, 12));
    }
}
