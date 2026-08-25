using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

namespace QuantumBuild.Tests.Unit.ToolboxTalks;

/// <summary>
/// Unit tests for TranscriptMarkerSanitizer - the output-sanitisation backstop that strips
/// residual [m:ss]/[h:mm:ss] caption-timestamp markers an LLM may echo into generated section
/// content (see CLAUDE.md's video-transcript-timestamps fix).
/// </summary>
public class TranscriptMarkerSanitizerTests
{
    [Theory]
    [InlineData("[0:00] Always wear your hard hat.", "Always wear your hard hat.")]
    [InlineData("[0:02] Check the harness before climbing.", "Check the harness before climbing.")]
    [InlineData("[12:34] Report any damage immediately.", "Report any damage immediately.")]
    public void StripTimestampMarkers_RemovesLeadingShortFormMarker(string input, string expected)
    {
        TranscriptMarkerSanitizer.StripTimestampMarkers(input).Should().Be(expected);
    }

    [Fact]
    public void StripTimestampMarkers_RemovesLongFormHourMinuteSecondMarker()
    {
        var input = "[1:02:15] Continue past the one-hour mark safely.";

        var result = TranscriptMarkerSanitizer.StripTimestampMarkers(input);

        result.Should().Be("Continue past the one-hour mark safely.");
    }

    [Fact]
    public void StripTimestampMarkers_RemovesMultipleMarkersAcrossLines()
    {
        var input = "[0:00] First point.\n[0:08] Second point.\n[0:16] Third point.";

        var result = TranscriptMarkerSanitizer.StripTimestampMarkers(input);

        result.Should().NotContain("[");
        result.Should().Contain("First point.");
        result.Should().Contain("Second point.");
        result.Should().Contain("Third point.");
    }

    [Fact]
    public void StripTimestampMarkers_WithNoMarkers_LeavesContentUnchanged()
    {
        var input = "Always wear your hard hat when on site. Report damage immediately.";

        var result = TranscriptMarkerSanitizer.StripTimestampMarkers(input);

        result.Should().Be(input);
    }

    [Fact]
    public void StripTimestampMarkers_DoesNotTouchUnbracketedTimeReferences()
    {
        // Legitimate content can mention times/durations without brackets - must survive untouched.
        var input = "Machine cycle time is 2:30 minutes; shift starts at 09:00.";

        var result = TranscriptMarkerSanitizer.StripTimestampMarkers(input);

        result.Should().Be(input);
    }

    [Fact]
    public void StripTimestampMarkers_DoesNotTouchNonTimestampBracketedContent()
    {
        // Bracketed content that isn't digit:digit shaped must not be over-matched.
        var input = "[Important] Wear PPE at all times. See [Appendix A] for details.";

        var result = TranscriptMarkerSanitizer.StripTimestampMarkers(input);

        result.Should().Be(input);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void StripTimestampMarkers_WithNullOrEmpty_ReturnsEmptyString(string? input)
    {
        TranscriptMarkerSanitizer.StripTimestampMarkers(input).Should().BeEmpty();
    }
}
