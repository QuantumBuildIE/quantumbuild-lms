using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Validation;

public class WordDiffServiceTests
{
    private readonly WordDiffService _sut = new();

    [Fact]
    public void Diff_IdenticalStrings_AllEqualOperations()
    {
        var result = _sut.Diff("the cat sat", "the cat sat");

        result.Operations.Should().HaveCount(3);
        result.Operations.Should().OnlyContain(op => op.Type == DiffType.Equal);
        result.MatchingWordCount.Should().Be(3);
        result.InsertedCount.Should().Be(0);
        result.DeletedCount.Should().Be(0);
        result.SimilarityPercentage.Should().Be(100.0);
    }

    [Fact]
    public void Diff_CompletelyDifferentStrings_AllInsertDelete()
    {
        var result = _sut.Diff("alpha beta", "gamma delta");

        result.Operations.Should().OnlyContain(op =>
            op.Type == DiffType.Insert || op.Type == DiffType.Delete);
        result.MatchingWordCount.Should().Be(0);
        result.InsertedCount.Should().Be(2);
        result.DeletedCount.Should().Be(2);
        result.SimilarityPercentage.Should().Be(0.0);
    }

    [Fact]
    public void Diff_OneWordChanged_EqualDeleteInsertEqual()
    {
        // "the cat sat" → "the dog sat"
        var result = _sut.Diff("the cat sat", "the dog sat");

        result.MatchingWordCount.Should().Be(2); // "the" and "sat"
        result.InsertedCount.Should().Be(1);     // "dog"
        result.DeletedCount.Should().Be(1);      // "cat"
        result.SimilarityPercentage.Should().BeApproximately(66.67, 0.1);

        // Check operation order: Equal(the), Delete(cat), Insert(dog), Equal(sat)
        var ops = result.Operations;
        ops.First().Type.Should().Be(DiffType.Equal);
        ops.First().Word.Should().Be("the");
        ops.Last().Type.Should().Be(DiffType.Equal);
        ops.Last().Word.Should().Be("sat");
    }

    [Fact]
    public void Diff_EmptyOriginal_AllInsertOperations()
    {
        var result = _sut.Diff("", "hello world");

        result.Operations.Should().HaveCount(2);
        result.Operations.Should().OnlyContain(op => op.Type == DiffType.Insert);
        result.InsertedCount.Should().Be(2);
        result.DeletedCount.Should().Be(0);
        result.MatchingWordCount.Should().Be(0);
        result.SimilarityPercentage.Should().Be(0.0);
    }

    [Fact]
    public void Diff_EmptyCandidate_AllDeleteOperations()
    {
        var result = _sut.Diff("hello world", "");

        result.Operations.Should().HaveCount(2);
        result.Operations.Should().OnlyContain(op => op.Type == DiffType.Delete);
        result.DeletedCount.Should().Be(2);
        result.InsertedCount.Should().Be(0);
        result.MatchingWordCount.Should().Be(0);
        result.SimilarityPercentage.Should().Be(0.0);
    }

    [Fact]
    public void Diff_BothEmpty_Returns100Similarity()
    {
        var result = _sut.Diff("", "");

        result.Operations.Should().BeEmpty();
        result.SimilarityPercentage.Should().Be(100.0);
    }

    [Fact]
    public void Diff_CaseInsensitiveMatching()
    {
        var result = _sut.Diff("PPE Required", "ppe required");

        result.MatchingWordCount.Should().Be(2);
        result.Operations.Should().OnlyContain(op => op.Type == DiffType.Equal);
        result.SimilarityPercentage.Should().Be(100.0);
    }

    [Fact]
    public void Diff_SummaryStatsCorrect()
    {
        // "a b c d" vs "a x c y" → matching: a, c = 2; deleted: b, d = 2; inserted: x, y = 2
        var result = _sut.Diff("a b c d", "a x c y");

        result.MatchingWordCount.Should().Be(2);
        result.InsertedCount.Should().Be(2);
        result.DeletedCount.Should().Be(2);
        // similarity: 2 / max(4, 4) * 100 = 50.0
        result.SimilarityPercentage.Should().Be(50.0);
    }
}
