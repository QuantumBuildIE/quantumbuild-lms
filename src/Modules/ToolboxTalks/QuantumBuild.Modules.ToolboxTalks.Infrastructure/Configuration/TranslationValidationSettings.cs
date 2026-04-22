namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for the Translation Validation feature.
/// Binds to the "TranslationValidation" section in appsettings.json.
/// </summary>
public class TranslationValidationSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "TranslationValidation";

    /// <summary>
    /// DeepL API settings for back-translation
    /// </summary>
    public DeepLSettings DeepL { get; set; } = new();

    /// <summary>
    /// Google Gemini API settings for back-translation
    /// </summary>
    public GeminiSettings Gemini { get; set; } = new();

    /// <summary>
    /// DeepSeek API settings — retained for reference only.
    /// Removed from active pipeline in v6.4 (GDPR: indefinite retention, China-based servers).
    /// Round 3 now uses Claude Sonnet via the existing Claude API key.
    /// </summary>
    [Obsolete("Removed in pipeline v6.4 — GDPR risk. Round 3 now uses Claude Sonnet.")]
    public DeepSeekSettings DeepSeek { get; set; } = new();

    /// <summary>
    /// Default pass threshold percentage (0-100).
    /// Default: 75
    /// </summary>
    public int DefaultThreshold { get; set; } = 75;

    /// <summary>
    /// Additional percentage points added to the threshold for safety-critical sections.
    /// Default: 10
    /// </summary>
    public int SafetyCriticalBump { get; set; } = 10;

    /// <summary>
    /// Maximum number of back-translation rounds before reaching a verdict.
    /// Default: 3
    /// </summary>
    public int MaxRounds { get; set; } = 3;

    /// <summary>
    /// Whether to run back-translation providers sequentially or in parallel.
    /// Values: "Sequential" or "Parallel"
    /// Default: Sequential
    /// </summary>
    public string ProcessingMode { get; set; } = "Sequential";

    /// <summary>
    /// Number of hours before a content creation session expires.
    /// Default: 24
    /// </summary>
    public int SessionExpiryHours { get; set; } = 24;
}

/// <summary>
/// DeepL API configuration for back-translation
/// </summary>
public class DeepLSettings
{
    /// <summary>
    /// DeepL API authentication key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// DeepL API base URL.
    /// Use https://api-free.deepl.com/v2 for free tier,
    /// or https://api.deepl.com/v2 for paid plans.
    /// Default: https://api-free.deepl.com/v2
    /// </summary>
    public string BaseUrl { get; set; } = "https://api-free.deepl.com/v2";
}

/// <summary>
/// Google Gemini API configuration for back-translation
/// </summary>
public class GeminiSettings
{
    /// <summary>
    /// Google AI API key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gemini model to use.
    /// Default: gemini-2.0-flash
    /// </summary>
    public string Model { get; set; } = "gemini-2.0-flash";

    /// <summary>
    /// Google AI API base URL.
    /// Default: https://generativelanguage.googleapis.com/v1beta
    /// </summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}

/// <summary>
/// DeepSeek API configuration for back-translation (OpenAI-compatible format)
/// </summary>
public class DeepSeekSettings
{
    /// <summary>
    /// DeepSeek API authentication key
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// DeepSeek API base URL.
    /// Default: https://api.deepseek.com/v1
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.deepseek.com/v1";

    /// <summary>
    /// DeepSeek model to use.
    /// Default: deepseek-chat
    /// </summary>
    public string Model { get; set; } = "deepseek-chat";
}
