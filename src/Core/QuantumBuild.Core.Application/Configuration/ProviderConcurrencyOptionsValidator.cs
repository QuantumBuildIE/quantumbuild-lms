using Microsoft.Extensions.Options;

namespace QuantumBuild.Core.Application.Configuration;

/// <summary>
/// Fail-fast startup validator for ProviderConcurrencyOptions.
/// Causes the application to throw at startup (not at first API call) when
/// an explicitly-set concurrency value is invalid. Baked-in C# defaults on
/// ProviderConcurrencyOptions already satisfy these constraints, so a missing
/// "ProviderConcurrency" config section never fails startup — only an
/// explicitly-set bad value does.
/// </summary>
public class ProviderConcurrencyOptionsValidator : IValidateOptions<ProviderConcurrencyOptions>
{
    public ValidateOptionsResult Validate(string? name, ProviderConcurrencyOptions options)
    {
        var errors = new List<string>();

        ValidateLimits("Anthropic", options.Anthropic, errors);
        ValidateLimits("DeepL", options.DeepL, errors);
        ValidateLimits("Gemini", options.Gemini, errors);

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateLimits(string providerName, ProviderConcurrencyLimits limits, List<string> errors)
    {
        if (limits.MaxConcurrency <= 0)
            errors.Add($"ProviderConcurrency:{providerName}:MaxConcurrency must be greater than 0");

        if (limits.MaxQueued < 0)
            errors.Add($"ProviderConcurrency:{providerName}:MaxQueued must not be negative");

        if (limits.SynchronousTimeoutSeconds is <= 0)
            errors.Add($"ProviderConcurrency:{providerName}:SynchronousTimeoutSeconds must be greater than 0 when set");
    }
}
