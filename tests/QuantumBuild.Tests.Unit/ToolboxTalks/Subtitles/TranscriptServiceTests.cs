using Microsoft.Extensions.Logging;
using Moq;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Subtitles;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Subtitles;

/// <summary>
/// Unit tests for TranscriptService, focused on the timestamp data-flow boundary between
/// caption/SRT use (timestamps required) and AI content-generation use (timestamps must be
/// stripped - see CLAUDE.md's video-transcript-timestamps fix).
/// </summary>
public class TranscriptServiceTests
{
    private readonly TranscriptService _sut;

    private const string ThreeCueSrt = """
        1
        00:00:00,000 --> 00:00:02,000
        Always wear your hard hat.

        2
        00:00:02,000 --> 00:00:05,000
        Check the harness before climbing.

        3
        00:00:05,000 --> 00:00:08,000
        Report any damage immediately.

        """;

    public TranscriptServiceTests()
    {
        var orchestrator = new Mock<ISubtitleProcessingOrchestrator>();
        var logger = new Mock<ILogger<TranscriptService>>();
        _sut = new TranscriptService(orchestrator.Object, logger.Object);
    }

    #region ParseSrtContent - subtitle/caption path must keep timestamps (no regression)

    [Fact]
    public void ParseSrtContent_FullText_StillContainsTimestampMarkers()
    {
        // The FullText produced for the caption/SRT round-trip is allowed to carry
        // [m:ss] markers - that is the legitimate structuring use documented in
        // docs/translation-scan-errors-recon.md §C. This fix must not touch it.
        var result = _sut.ParseSrtContent(ThreeCueSrt);

        result.Success.Should().BeTrue();
        result.FullText.Should().Contain("[0:00]");
        result.FullText.Should().Contain("[0:02]");
        result.FullText.Should().Contain("[0:05]");
    }

    [Fact]
    public void FormatForAi_StillContainsTimestampMarkers()
    {
        var parsed = _sut.ParseSrtContent(ThreeCueSrt);

        var formatted = _sut.FormatForAi(parsed);

        formatted.Should().Contain("[0:00");
        formatted.Should().Contain("[0:02");
    }

    #endregion

    #region GetCleanFullText - content-generation path must be timestamp-free

    [Fact]
    public void GetCleanFullText_WithTimestampedTranscript_ContainsNoTimestampMarkers()
    {
        var parsed = _sut.ParseSrtContent(ThreeCueSrt);

        var clean = _sut.GetCleanFullText(parsed);

        clean.Should().NotContain("[0:00]");
        clean.Should().NotContain("[0:02]");
        clean.Should().NotContain("[0:05]");
        clean.Should().NotContain("[");
        clean.Should().NotContain("]");
    }

    [Fact]
    public void GetCleanFullText_WithTimestampedTranscript_PreservesActualWords()
    {
        var parsed = _sut.ParseSrtContent(ThreeCueSrt);

        var clean = _sut.GetCleanFullText(parsed);

        clean.Should().Contain("Always wear your hard hat.");
        clean.Should().Contain("Check the harness before climbing.");
        clean.Should().Contain("Report any damage immediately.");
    }

    [Fact]
    public void GetCleanFullText_WithFailedTranscript_ReturnsEmptyString()
    {
        var failed = TranscriptResult.FailureResult("no transcript");

        var clean = _sut.GetCleanFullText(failed);

        clean.Should().BeEmpty();
    }

    [Fact]
    public void GetCleanFullText_WithNoSegments_ReturnsEmptyString()
    {
        var empty = TranscriptResult.SuccessResult(string.Empty, new List<TranscriptSegment>(), TimeSpan.Zero);

        var clean = _sut.GetCleanFullText(empty);

        clean.Should().BeEmpty();
    }

    [Fact]
    public void GetCleanFullText_OrdersSegmentsByIndex()
    {
        var segments = new List<TranscriptSegment>
        {
            new(Index: 2, StartTime: TimeSpan.FromSeconds(5), EndTime: TimeSpan.FromSeconds(8), Text: "second", PercentageIntoVideo: 50),
            new(Index: 1, StartTime: TimeSpan.Zero, EndTime: TimeSpan.FromSeconds(5), Text: "first", PercentageIntoVideo: 0)
        };
        var transcript = TranscriptResult.SuccessResult("unused", segments, TimeSpan.FromSeconds(8));

        var clean = _sut.GetCleanFullText(transcript);

        clean.Should().Be("first second");
    }

    #endregion
}
