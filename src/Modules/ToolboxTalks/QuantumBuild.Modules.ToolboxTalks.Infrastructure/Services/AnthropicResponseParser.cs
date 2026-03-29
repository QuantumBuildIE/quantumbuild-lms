using System.Text.Json;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services;

public record AnthropicParsedResponse(
    string ContentText,
    int InputTokens,
    int OutputTokens,
    string Model);

public static class AnthropicResponseParser
{
    public static AnthropicParsedResponse Parse(string responseBody)
    {
        using var jsonDoc = JsonDocument.Parse(responseBody);
        var root = jsonDoc.RootElement;

        // Extract content[0].text
        var contentText = string.Empty;
        if (root.TryGetProperty("content", out var contentArray))
        {
            foreach (var item in contentArray.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var textEl))
                {
                    contentText = textEl.GetString() ?? string.Empty;
                    break;
                }
            }
        }

        // Extract usage.input_tokens and usage.output_tokens
        var inputTokens = 0;
        var outputTokens = 0;
        if (root.TryGetProperty("usage", out var usageEl))
        {
            if (usageEl.TryGetProperty("input_tokens", out var inputEl))
                inputTokens = inputEl.GetInt32();
            if (usageEl.TryGetProperty("output_tokens", out var outputEl))
                outputTokens = outputEl.GetInt32();
        }

        // Extract model
        var model = string.Empty;
        if (root.TryGetProperty("model", out var modelEl))
            model = modelEl.GetString() ?? string.Empty;

        return new AnthropicParsedResponse(contentText, inputTokens, outputTokens, model);
    }
}
