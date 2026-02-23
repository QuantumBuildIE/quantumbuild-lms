namespace QuantumBuild.Core.Application.Abstractions.AI;

/// <summary>
/// Claude API configuration shared across modules.
/// Binds to the "SubtitleProcessing:Claude" section in appsettings.json.
/// </summary>
public class ClaudeSettings
{
    /// <summary>
    /// Configuration section path in appsettings.json
    /// </summary>
    public const string SectionName = "SubtitleProcessing:Claude";

    /// <summary>
    /// Anthropic API key for Claude
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Claude model to use.
    /// Default: claude-sonnet-4-20250514
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>
    /// Maximum tokens for responses.
    /// Default: 4000
    /// </summary>
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Anthropic API base URL.
    /// Default: https://api.anthropic.com/v1
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com/v1";
}
