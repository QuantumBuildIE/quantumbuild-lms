using FluentAssertions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Validation;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;
using Xunit;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Validation;

public class DiffRunGrouperTests
{
    private readonly DiffRunGrouper _sut = new();

    private static DiffOperation Op(DiffType type, string word = "w") =>
        new() { Type = type, Word = word };

    [Fact]
    public void Group_EmptyOperations_ReturnsEmpty()
    {
        var result = _sut.Group([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Group_AllEqualOperations_ReturnsEmpty()
    {
        var result = _sut.Group([Op(DiffType.Equal), Op(DiffType.Equal), Op(DiffType.Equal)]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Group_SingleDelete_ReturnsEmpty_BelowThreshold()
    {
        var result = _sut.Group([Op(DiffType.Delete)]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Group_TwoConsecutiveDeletes_ReturnsOneDiffRun()
    {
        var result = _sut.Group([Op(DiffType.Delete, "a"), Op(DiffType.Delete, "b")]);

        result.Should().HaveCount(1);
        result[0].Should().Be(new DiffRun(0, 1, DiffType.Delete));
    }

    [Fact]
    public void Group_ThreeConsecutiveInserts_ReturnsOneInsertRun()
    {
        // All three inserts point to the same original-word position (counter stays at 0)
        var result = _sut.Group(
        [
            Op(DiffType.Insert, "x"), Op(DiffType.Insert, "y"), Op(DiffType.Insert, "z")
        ]);

        result.Should().HaveCount(1);
        result[0].Should().Be(new DiffRun(0, 0, DiffType.Insert));
    }

    [Fact]
    public void Group_MixedDeleteInsertDeleteEachLengthOne_ReturnsEmpty()
    {
        // Each run is length 1 → below threshold
        var result = _sut.Group(
        [
            Op(DiffType.Delete, "a"),
            Op(DiffType.Insert, "x"),
            Op(DiffType.Delete, "b")
        ]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Group_TwoDeletesOneEqualTwoDeletes_ReturnsTwoSeparateRuns()
    {
        // Equal resets the run; each Delete group is independent
        var result = _sut.Group(
        [
            Op(DiffType.Delete, "a"), Op(DiffType.Delete, "b"),
            Op(DiffType.Equal, "c"),
            Op(DiffType.Delete, "d"), Op(DiffType.Delete, "e")
        ]);

        result.Should().HaveCount(2);
        result[0].Should().Be(new DiffRun(0, 1, DiffType.Delete));
        result[1].Should().Be(new DiffRun(3, 4, DiffType.Delete));
    }

    [Fact]
    public void Group_TwoDeletesFollowedByTwoInserts_ReturnsTwoRunsDifferentTypes()
    {
        var result = _sut.Group(
        [
            Op(DiffType.Delete, "a"), Op(DiffType.Delete, "b"),
            Op(DiffType.Insert, "x"), Op(DiffType.Insert, "y")
        ]);

        result.Should().HaveCount(2);
        result[0].Should().Be(new DiffRun(0, 1, DiffType.Delete));
        // Inserts follow the two deletes; counter is 2 at the insertion point
        result[1].Should().Be(new DiffRun(2, 2, DiffType.Insert));
    }

    [Fact]
    public void Group_LongDeleteRun_ReturnsOneDiffRunSpanningAll()
    {
        var result = _sut.Group(
        [
            Op(DiffType.Delete, "a"), Op(DiffType.Delete, "b"), Op(DiffType.Delete, "c"),
            Op(DiffType.Delete, "d"), Op(DiffType.Delete, "e")
        ]);

        result.Should().HaveCount(1);
        result[0].Should().Be(new DiffRun(0, 4, DiffType.Delete));
    }

    [Fact]
    public void Group_InsertAtStartOfOperations_OriginalWordIndexIsZero()
    {
        // No Equal or Delete precedes the inserts → counter is 0
        var result = _sut.Group([Op(DiffType.Insert, "x"), Op(DiffType.Insert, "y")]);

        result.Should().HaveCount(1);
        result[0].Should().Be(new DiffRun(0, 0, DiffType.Insert));
    }

    [Fact]
    public void Group_InsertAtEndOfOperations_OriginalWordIndexIsLastPlusOne()
    {
        // Two Equals advance counter to 2; inserts reference that position ("last + 1")
        var result = _sut.Group(
        [
            Op(DiffType.Equal, "a"), Op(DiffType.Equal, "b"),
            Op(DiffType.Insert, "x"), Op(DiffType.Insert, "y")
        ]);

        result.Should().HaveCount(1);
        result[0].Should().Be(new DiffRun(2, 2, DiffType.Insert));
    }
}
