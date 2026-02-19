using System.Text;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Translations;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Prompts;

/// <summary>
/// Centralized prompts for AI content and subtitle translation.
/// </summary>
public static class TranslationPrompts
{
    /// <summary>
    /// Builds the prompt for translating plain text or HTML content.
    /// </summary>
    public static string BuildContentTranslationPrompt(string text, string sourceLanguage, string targetLanguage, bool isHtml)
    {
        if (isHtml)
        {
            return $@"Translate the following {sourceLanguage} HTML content to {targetLanguage}.
IMPORTANT: Keep all HTML tags exactly as they are. Only translate the text content between tags.
Return only the translated HTML, nothing else.

{text}";
        }

        return $@"Translate the following {sourceLanguage} text to {targetLanguage}.
Return only the translated text, nothing else.

{text}";
    }

    /// <summary>
    /// Builds the prompt for batch translating multiple items.
    /// </summary>
    public static string BuildBatchTranslationPrompt(List<TranslationItem> items, string sourceLanguage, string targetLanguage)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Translate the following {sourceLanguage} items to {targetLanguage}.");
        sb.AppendLine("Return the translations as a JSON array with the same order as the input.");
        sb.AppendLine("Each element should be the translated text only.");
        sb.AppendLine("For HTML content (marked with [HTML]), preserve all HTML tags and only translate the text.");
        sb.AppendLine();
        sb.AppendLine("Items to translate:");
        sb.AppendLine("```");

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var prefix = item.IsHtml ? "[HTML] " : "";
            var context = !string.IsNullOrEmpty(item.Context) ? $" ({item.Context})" : "";
            sb.AppendLine($"{i + 1}. {prefix}{item.Text}{context}");
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("Return only a valid JSON array of translated strings, like:");
        sb.AppendLine("[\"translated text 1\", \"translated text 2\", ...]");

        return sb.ToString();
    }

    /// <summary>
    /// Builds the prompt for translating SRT subtitle content.
    /// </summary>
    public static string BuildSrtTranslationPrompt(string srtContent, string targetLanguage)
    {
        return $@"Translate the following SRT subtitle text to {targetLanguage}.
Keep the exact same format with numbers and timestamps, only translate the text.
Return only the translated SRT, nothing else:

{srtContent}";
    }
}
