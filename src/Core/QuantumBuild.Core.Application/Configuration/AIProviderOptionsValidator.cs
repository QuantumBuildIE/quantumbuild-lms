using Microsoft.Extensions.Options;

namespace QuantumBuild.Core.Application.Configuration;

/// <summary>
/// Fail-fast startup validator for AIProviderOptions.
/// Causes the application to throw at startup (not at first API call) when
/// any required model identifier is missing from AIProviders config.
/// </summary>
public class AIProviderOptionsValidator : IValidateOptions<AIProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, AIProviderOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Anthropic.Models.Sonnet))
            errors.Add("AIProviders:Anthropic:Models:Sonnet must be set");
        if (string.IsNullOrWhiteSpace(options.Anthropic.Models.Haiku))
            errors.Add("AIProviders:Anthropic:Models:Haiku must be set");
        if (string.IsNullOrWhiteSpace(options.Gemini.Models.Flash))
            errors.Add("AIProviders:Gemini:Models:Flash must be set");
        if (string.IsNullOrWhiteSpace(options.ElevenLabs.Models.Transcription))
            errors.Add("AIProviders:ElevenLabs:Models:Transcription must be set");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
