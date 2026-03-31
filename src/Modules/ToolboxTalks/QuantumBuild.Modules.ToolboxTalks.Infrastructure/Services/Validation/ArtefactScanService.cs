using System.Text.RegularExpressions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.ArtefactScan;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Validation;

public partial class ArtefactScanService : IArtefactScanService
{
    private static readonly HashSet<string> AllowedEnglishTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "GPS", "HIQA", "EVV", "HACCP", "PPE", "HSA", "FSAI", "RSA",
        "COVID", "PDF", "URL", "HTTP", "GDPR"
    };

    public ArtefactScanResult Scan(string originalText, string translatedText, string targetLanguage)
    {
        var artefacts = new List<DetectedArtefact>();

        CheckUntranslatedEnglish(originalText, translatedText, artefacts);
        CheckPossibleTruncation(originalText, translatedText, artefacts);
        CheckDuplicatedPhrase(translatedText, artefacts);
        CheckCollapsedBulletList(translatedText, artefacts);
        CheckStrayNumber(translatedText, artefacts);

        return new ArtefactScanResult(artefacts.AsReadOnly(), artefacts.Count > 0);
    }

    private static void CheckUntranslatedEnglish(
        string originalText, string translatedText, List<DetectedArtefact> artefacts)
    {
        var originalWords = ExtractWords(originalText);
        var translatedWords = ExtractWords(translatedText);

        var carriedOver = translatedWords
            .Where(w => originalWords.Contains(w) && !AllowedEnglishTerms.Contains(w))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (carriedOver.Count > 2)
        {
            var sample = string.Join(", ", carriedOver.Take(5));
            artefacts.Add(new DetectedArtefact(ArtefactType.UntranslatedEnglish, sample));
        }
    }

    private static void CheckPossibleTruncation(
        string originalText, string translatedText, List<DetectedArtefact> artefacts)
    {
        if (originalText.Length > 200 && translatedText.Length < 0.4 * originalText.Length)
        {
            var percent = (int)Math.Round((double)translatedText.Length / originalText.Length * 100);
            artefacts.Add(new DetectedArtefact(
                ArtefactType.PossibleTruncation,
                $"Translation is {percent}% length of source"));
        }
    }

    private static void CheckDuplicatedPhrase(
        string translatedText, List<DetectedArtefact> artefacts)
    {
        var words = translatedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 8) return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i <= words.Length - 4; i++)
        {
            var phrase = string.Join(" ", words[i], words[i + 1], words[i + 2], words[i + 3]);
            if (phrase.Length <= 15) continue;

            if (!seen.Add(phrase))
            {
                artefacts.Add(new DetectedArtefact(
                    ArtefactType.DuplicatedPhrase,
                    $"\"{phrase}\""));
                return;
            }
        }
    }

    private static void CheckCollapsedBulletList(
        string translatedText, List<DetectedArtefact> artefacts)
    {
        var paragraphs = translatedText.Split(
            new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var paragraph in paragraphs)
        {
            var lines = paragraph.Split(
                new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

            var bulletCount = lines.Count(line =>
            {
                var trimmed = line.TrimStart();
                return trimmed.StartsWith("- ") || trimmed.StartsWith("• ");
            });

            if (bulletCount >= 4)
            {
                artefacts.Add(new DetectedArtefact(
                    ArtefactType.CollapsedBulletList,
                    $"{bulletCount} dash-separated items found in single paragraph"));
                return;
            }
        }
    }

    private static void CheckStrayNumber(
        string translatedText, List<DetectedArtefact> artefacts)
    {
        var matches = StrayNumberRegex().Matches(translatedText);
        foreach (Match match in matches)
        {
            var context = match.Value.Trim();
            artefacts.Add(new DetectedArtefact(ArtefactType.StrayNumber, context));
            return;
        }
    }

    private static HashSet<string> ExtractWords(string text)
    {
        return WordRegex().Matches(text)
            .Select(m => m.Value)
            .Where(w => w.Length >= 4)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\b[a-zA-Z]+\b")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"(?<=\p{L}\s)\d+(?!\s*[%/\-\.]\d)(?!\s*(?:st|nd|rd|th)\b)", RegexOptions.Compiled)]
    private static partial Regex StrayNumberRegex();
}
