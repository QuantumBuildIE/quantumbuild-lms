namespace QuantumBuild.Core.Application.Configuration;

/// <summary>
/// Per-provider concurrency ceilings for external AI/translation HTTP calls
/// (Anthropic, DeepL, Gemini), enforced via Polly Bulkhead policies.
/// Binds to the "ProviderConcurrency" section in appsettings.json.
/// Defaults are baked in below so a missing/partial config section does not
/// fail ValidateOnStart() — only an explicitly-set invalid value should fail.
/// </summary>
public class ProviderConcurrencyOptions
{
    public const string SectionName = "ProviderConcurrency";

    /// <summary>Anthropic (Claude) — shared across ~13 services/typed clients that all draw on one API key/quota.</summary>
    public ProviderConcurrencyLimits Anthropic { get; set; } = new()
    {
        MaxConcurrency = 5,
        MaxQueued = 20,
        SynchronousTimeoutSeconds = 30
    };

    /// <summary>DeepL — only consumed via ConsensusEngine Round 1 back-translation.</summary>
    public ProviderConcurrencyLimits DeepL { get; set; } = new()
    {
        MaxConcurrency = 10,
        MaxQueued = 40
    };

    /// <summary>Gemini — only consumed via ConsensusEngine Round 2 back-translation.</summary>
    public ProviderConcurrencyLimits Gemini { get; set; } = new()
    {
        MaxConcurrency = 5,
        MaxQueued = 20
    };
}

/// <summary>Concurrency ceiling for one provider.</summary>
public class ProviderConcurrencyLimits
{
    /// <summary>Max simultaneous in-flight HTTP calls to this provider.</summary>
    public int MaxConcurrency { get; set; }

    /// <summary>Max callers allowed to queue waiting for a permit before Polly rejects with BulkheadRejectedException.</summary>
    public int MaxQueued { get; set; }

    /// <summary>
    /// Only meaningful for a provider that also has a synchronous (request-path) caller.
    /// Null means "no synchronous-timeout variant is used for this provider."
    /// </summary>
    public int? SynchronousTimeoutSeconds { get; set; }
}
