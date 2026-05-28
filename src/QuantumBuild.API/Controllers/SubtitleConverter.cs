namespace QuantumBuild.API.Controllers;

internal static class SubtitleConverter
{
    internal static string SrtToVtt(string srtContent)
    {
        var lines = srtContent.Split('\n');
        var vttLines = new List<string> { "WEBVTT", "" };

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd('\r');

            // Convert timestamp format: 00:00:00,000 --> 00:00:00.000
            if (trimmedLine.Contains(" --> "))
                vttLines.Add(trimmedLine.Replace(',', '.'));
            else
                vttLines.Add(trimmedLine);
        }

        return string.Join("\n", vttLines);
    }
}
