using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Subtitles;

/// <summary>
/// Unit tests for SrtGeneratorService
/// </summary>
public class SrtGeneratorServiceTests
{
    private readonly SrtGeneratorService _sut;

    public SrtGeneratorServiceTests()
    {
        _sut = new SrtGeneratorService();
    }

    #region GenerateSrt Tests

    [Fact]
    public void GenerateSrt_WithEmptyWords_ReturnsEmptyString()
    {
        // Arrange
        var words = new List<TranscriptWord>();

        // Act
        var result = _sut.GenerateSrt(words);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GenerateSrt_WithSingleWord_GeneratesOneSubtitle()
    {
        // Arrange
        var words = new List<TranscriptWord>
        {
            new() { Text = "Hello", Type = "word", Start = 0.0m, End = 0.5m }
        };

        // Act
        var result = _sut.GenerateSrt(words, wordsPerSubtitle: 8);

        // Assert
        result.Should().Contain("1");
        result.Should().Contain("00:00:00,000 --> 00:00:00,500");
        result.Should().Contain("Hello");
    }

    [Fact]
    public void GenerateSrt_WithMultipleWords_GroupsWordsCorrectly()
    {
        // Arrange
        var words = new List<TranscriptWord>
        {
            new() { Text = "This", Type = "word", Start = 0.0m, End = 0.3m },
            new() { Text = "is", Type = "word", Start = 0.35m, End = 0.5m },
            new() { Text = "a", Type = "word", Start = 0.55m, End = 0.6m },
            new() { Text = "test.", Type = "word", Start = 0.65m, End = 1.0m }
        };

        // Act
        var result = _sut.GenerateSrt(words, wordsPerSubtitle: 8);

        // Assert
        result.Should().Contain("This is a test.");
        result.Should().Contain("00:00:00,000 --> 00:00:01,000");
    }

    [Fact]
    public void GenerateSrt_BreaksAtPunctuation()
    {
        // Arrange
        var words = new List<TranscriptWord>
        {
            new() { Text = "First", Type = "word", Start = 0.0m, End = 0.3m },
            new() { Text = "sentence.", Type = "word", Start = 0.35m, End = 0.7m },
            new() { Text = "Second", Type = "word", Start = 0.75m, End = 1.0m },
            new() { Text = "sentence.", Type = "word", Start = 1.05m, End = 1.5m }
        };

        // Act
        var result = _sut.GenerateSrt(words, wordsPerSubtitle: 10);

        // Assert
        var blocks = _sut.SplitSrtIntoBlocks(result);
        blocks.Should().HaveCount(2);
        blocks[0].Should().Contain("First sentence.");
        blocks[1].Should().Contain("Second sentence.");
    }

    [Fact]
    public void GenerateSrt_BreaksAtQuestionMark()
    {
        // Arrange
        var words = new List<TranscriptWord>
        {
            new() { Text = "How", Type = "word", Start = 0.0m, End = 0.2m },
            new() { Text = "are", Type = "word", Start = 0.25m, End = 0.4m },
            new() { Text = "you?", Type = "word", Start = 0.45m, End = 0.8m },
            new() { Text = "Great!", Type = "word", Start = 1.0m, End = 1.5m }
        };

        // Act
        var result = _sut.GenerateSrt(words, wordsPerSubtitle: 10);

        // Assert
        var blocks = _sut.SplitSrtIntoBlocks(result);
        blocks.Should().HaveCount(2);
        blocks[0].Should().Contain("How are you?");
        blocks[1].Should().Contain("Great!");
    }

    [Fact]
    public void GenerateSrt_SkipsSpacingElements()
    {
        // Arrange
        var words = new List<TranscriptWord>
        {
            new() { Text = "Hello", Type = "word", Start = 0.0m, End = 0.3m },
            new() { Text = " ", Type = "spacing", Start = 0.35m, End = 0.4m },
            new() { Text = "World", Type = "word", Start = 0.45m, End = 0.8m }
        };

        // Act
        var result = _sut.GenerateSrt(words, wordsPerSubtitle: 8);

        // Assert
        result.Should().Contain("Hello World");
        result.Should().NotContain("spacing");
    }

    [Fact]
    public void GenerateSrt_SkipsAudioEvents()
    {
        // Arrange
        var words = new List<TranscriptWord>
        {
            new() { Text = "Hello", Type = "word", Start = 0.0m, End = 0.3m },
            new() { Text = "(music)", Type = "audio_event", Start = 0.35m, End = 0.5m },
            new() { Text = "World", Type = "word", Start = 0.55m, End = 0.9m }
        };

        // Act
        var result = _sut.GenerateSrt(words, wordsPerSubtitle: 8);

        // Assert
        result.Should().Contain("Hello World");
        result.Should().NotContain("(music)");
    }

    [Fact]
    public void GenerateSrt_SkipsEmptyWords()
    {
        // Arrange
        var words = new List<TranscriptWord>
        {
            new() { Text = "Hello", Type = "word", Start = 0.0m, End = 0.3m },
            new() { Text = "", Type = "word", Start = 0.35m, End = 0.4m },
            new() { Text = "   ", Type = "word", Start = 0.45m, End = 0.5m },
            new() { Text = "World", Type = "word", Start = 0.55m, End = 0.9m }
        };

        // Act
        var result = _sut.GenerateSrt(words, wordsPerSubtitle: 8);

        // Assert
        result.Should().Contain("Hello World");
    }

    [Fact]
    public void GenerateSrt_RespectsWordsPerSubtitleLimit()
    {
        // Arrange
        var words = new List<TranscriptWord>();
        for (int i = 0; i < 20; i++)
        {
            words.Add(new TranscriptWord
            {
                Text = $"Word{i}",
                Type = "word",
                Start = i * 0.5m,
                End = (i * 0.5m) + 0.4m
            });
        }

        // Act
        var result = _sut.GenerateSrt(words, wordsPerSubtitle: 5);

        // Assert
        var blocks = _sut.SplitSrtIntoBlocks(result);
        blocks.Should().HaveCount(4); // 20 words / 5 per subtitle = 4 blocks
    }

    [Fact]
    public void GenerateSrt_FormatsTimestampsCorrectly()
    {
        // Arrange
        var words = new List<TranscriptWord>
        {
            new() { Text = "Test", Type = "word", Start = 65.123m, End = 66.789m } // 1 minute 5 seconds
        };

        // Act
        var result = _sut.GenerateSrt(words);

        // Assert
        result.Should().Contain("00:01:05,123 --> 00:01:06,789");
    }

    [Fact]
    public void GenerateSrt_HandlesHoursInTimestamp()
    {
        // Arrange
        var words = new List<TranscriptWord>
        {
            new() { Text = "Test", Type = "word", Start = 3661.500m, End = 3662.750m } // 1 hour, 1 minute, 1.5 seconds
        };

        // Act
        var result = _sut.GenerateSrt(words);

        // Assert
        result.Should().Contain("01:01:01,500 --> 01:01:02,750");
    }

    #endregion

    #region SplitSrtIntoBlocks Tests

    [Fact]
    public void SplitSrtIntoBlocks_WithValidSrt_ReturnsCorrectBlocks()
    {
        // Arrange
        var srtContent = """
            1
            00:00:00,000 --> 00:00:02,000
            First subtitle

            2
            00:00:02,500 --> 00:00:04,000
            Second subtitle

            3
            00:00:04,500 --> 00:00:06,000
            Third subtitle

            """;

        // Act
        var blocks = _sut.SplitSrtIntoBlocks(srtContent);

        // Assert
        blocks.Should().HaveCount(3);
        blocks[0].Should().Contain("First subtitle");
        blocks[1].Should().Contain("Second subtitle");
        blocks[2].Should().Contain("Third subtitle");
    }

    [Fact]
    public void SplitSrtIntoBlocks_WithEmptyContent_ReturnsEmptyList()
    {
        // Arrange
        var srtContent = "";

        // Act
        var blocks = _sut.SplitSrtIntoBlocks(srtContent);

        // Assert
        blocks.Should().BeEmpty();
    }

    [Fact]
    public void SplitSrtIntoBlocks_WithWhitespaceOnly_ReturnsEmptyList()
    {
        // Arrange
        var srtContent = "   \n\n   \n\n   ";

        // Act
        var blocks = _sut.SplitSrtIntoBlocks(srtContent);

        // Assert
        blocks.Should().BeEmpty();
    }

    [Fact]
    public void SplitSrtIntoBlocks_HandlesCRLFLineEndings()
    {
        // Arrange
        var srtContent = "1\r\n00:00:00,000 --> 00:00:02,000\r\nFirst\r\n\r\n2\r\n00:00:02,500 --> 00:00:04,000\r\nSecond\r\n\r\n";

        // Act
        var blocks = _sut.SplitSrtIntoBlocks(srtContent);

        // Assert
        blocks.Should().HaveCount(2);
    }

    #endregion

    #region CountSubtitleBlocks Tests

    [Fact]
    public void CountSubtitleBlocks_WithValidSrt_ReturnsCorrectCount()
    {
        // Arrange
        var srtContent = """
            1
            00:00:00,000 --> 00:00:02,000
            First subtitle

            2
            00:00:02,500 --> 00:00:04,000
            Second subtitle

            """;

        // Act
        var count = _sut.CountSubtitleBlocks(srtContent);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void CountSubtitleBlocks_WithEmptyContent_ReturnsZero()
    {
        // Arrange
        var srtContent = "";

        // Act
        var count = _sut.CountSubtitleBlocks(srtContent);

        // Assert
        count.Should().Be(0);
    }

    #endregion
}
