namespace QuantumBuild.Core.Application.Configuration;

/// <summary>
/// Canonical registry of model identifiers for all external AI/ML providers.
/// Binds to the "AIProviders" section in appsettings.json.
/// Changing a model identifier requires only an env var update — no code deploy.
/// </summary>
public class AIProviderOptions
{
    public const string SectionName = "AIProviders";

    /// <summary>Anthropic / Claude model identifiers.</summary>
    public AnthropicProviderOptions Anthropic { get; set; } = new();

    /// <summary>Google Gemini model identifiers.</summary>
    public GeminiProviderOptions Gemini { get; set; } = new();

    /// <summary>ElevenLabs model identifiers.</summary>
    public ElevenLabsProviderOptions ElevenLabs { get; set; } = new();
}

/// <summary>Anthropic / Claude model identifiers.</summary>
public class AnthropicProviderOptions
{
    public AnthropicModels Models { get; set; } = new();
}

/// <summary>Named Anthropic model identifiers used in the codebase.</summary>
public class AnthropicModels
{
    /// <summary>Current Claude Sonnet model identifier (e.g. claude-sonnet-4-5).</summary>
    public string Sonnet { get; set; } = string.Empty;

    /// <summary>Current Claude Haiku model identifier (e.g. claude-haiku-4-5-20251001).</summary>
    public string Haiku { get; set; } = string.Empty;
}

/// <summary>Google Gemini model identifiers.</summary>
public class GeminiProviderOptions
{
    public GeminiModels Models { get; set; } = new();
}

/// <summary>Named Gemini model identifiers used in the codebase.</summary>
public class GeminiModels
{
    /// <summary>Current Gemini Flash model identifier (e.g. gemini-2.0-flash).</summary>
    public string Flash { get; set; } = string.Empty;
}

/// <summary>ElevenLabs model identifiers.</summary>
public class ElevenLabsProviderOptions
{
    public ElevenLabsModels Models { get; set; } = new();
}

/// <summary>Named ElevenLabs model identifiers used in the codebase.</summary>
public class ElevenLabsModels
{
    /// <summary>Current transcription model identifier (e.g. scribe_v1).</summary>
    public string Transcription { get; set; } = string.Empty;
}
