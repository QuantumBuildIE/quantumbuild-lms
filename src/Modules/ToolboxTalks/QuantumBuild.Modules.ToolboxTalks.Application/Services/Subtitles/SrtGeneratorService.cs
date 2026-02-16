using System.Text;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;

/// <summary>
/// Service for generating SRT subtitle files from ElevenLabs transcription data.
/// </summary>
public class SrtGeneratorService : ISrtGeneratorService
{
    /// <summary>
    /// Generates SRT content from transcript words.
    /// Groups words into chunks of ~wordsPerSubtitle, breaking at punctuation.
    /// </summary>
    public string GenerateSrt(List<TranscriptWord> words, int wordsPerSubtitle = 8)
    {
        var srt = new StringBuilder();
        var counter = 1;

        var chunkText = new StringBuilder();
        decimal chunkStart = 0;
        decimal chunkEnd = 0;
        int wordCount = 0;

        foreach (var word in words)
        {
            // Skip spacing and audio events
            if (word.Type == "spacing" || word.Type == "audio_event")
                continue;

            // Skip empty words
            if (string.IsNullOrWhiteSpace(word.Text))
                continue;

            if (wordCount == 0)
                chunkStart = word.Start;

            chunkText.Append(word.Text).Append(' ');
            chunkEnd = word.End;
            wordCount++;

            // Create subtitle every N words or at punctuation
            bool isPunctuation = word.Text.EndsWith(".") || word.Text.EndsWith("?") || word.Text.EndsWith("!");

            if (wordCount >= wordsPerSubtitle || isPunctuation)
            {
                var displayText = chunkText.ToString().Trim();

                if (!string.IsNullOrEmpty(displayText))
                {
                    srt.AppendLine(counter.ToString());
                    srt.AppendLine($"{FormatTimestamp(chunkStart)} --> {FormatTimestamp(chunkEnd)}");
                    srt.AppendLine(displayText);
                    srt.AppendLine();
                    counter++;
                }

                chunkText.Clear();
                wordCount = 0;
            }
        }

        // Handle remaining words
        if (wordCount > 0)
        {
            var displayText = chunkText.ToString().Trim();
            if (!string.IsNullOrEmpty(displayText))
            {
                srt.AppendLine(counter.ToString());
                srt.AppendLine($"{FormatTimestamp(chunkStart)} --> {FormatTimestamp(chunkEnd)}");
                srt.AppendLine(displayText);
                srt.AppendLine();
            }
        }

        return srt.ToString();
    }

    /// <summary>
    /// Splits SRT content into individual subtitle blocks for batch translation.
    /// </summary>
    public List<string> SplitSrtIntoBlocks(string srtContent)
    {
        return srtContent
            .Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Where(block => !string.IsNullOrWhiteSpace(block))
            .ToList();
    }

    /// <summary>
    /// Counts the number of subtitle blocks in SRT content.
    /// </summary>
    public int CountSubtitleBlocks(string srtContent)
    {
        return SplitSrtIntoBlocks(srtContent).Count;
    }

    /// <summary>
    /// Formats decimal seconds to SRT timestamp: HH:MM:SS,mmm
    /// </summary>
    private static string FormatTimestamp(decimal seconds)
    {
        var totalMs = (int)(seconds * 1000);
        var ms = totalMs % 1000;
        var totalSec = totalMs / 1000;
        var s = totalSec % 60;
        var totalMin = totalSec / 60;
        var m = totalMin % 60;
        var h = totalMin / 60;

        return $"{h:D2}:{m:D2}:{s:D2},{ms:D3}";
    }
}
