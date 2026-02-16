using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;

/// <summary>
/// Service for generating SRT subtitle files from transcription data.
/// </summary>
public interface ISrtGeneratorService
{
    /// <summary>
    /// Generates SRT content from transcript words.
    /// Groups words into chunks of approximately wordsPerSubtitle,
    /// breaking at punctuation for natural reading flow.
    /// </summary>
    /// <param name="words">List of transcript words with timing data</param>
    /// <param name="wordsPerSubtitle">Target number of words per subtitle entry (default: 8)</param>
    /// <returns>SRT formatted string</returns>
    string GenerateSrt(List<TranscriptWord> words, int wordsPerSubtitle = 8);

    /// <summary>
    /// Splits SRT content into individual subtitle blocks for batch translation.
    /// </summary>
    /// <param name="srtContent">Full SRT content</param>
    /// <returns>List of individual subtitle blocks</returns>
    List<string> SplitSrtIntoBlocks(string srtContent);

    /// <summary>
    /// Counts the number of subtitle blocks in SRT content.
    /// </summary>
    /// <param name="srtContent">SRT content to count</param>
    /// <returns>Number of subtitle blocks</returns>
    int CountSubtitleBlocks(string srtContent);
}
