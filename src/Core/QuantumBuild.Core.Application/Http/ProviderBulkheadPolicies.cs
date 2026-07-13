using Microsoft.Extensions.Options;
using Polly;
using QuantumBuild.Core.Application.Configuration;

namespace QuantumBuild.Core.Application.Http;

/// <summary>
/// Holds ONE shared Bulkhead policy instance per external AI/translation provider.
/// Registered as a DI singleton (AddSingleton&lt;ProviderBulkheadPolicies&gt;()) so every
/// HttpClient registration for the same provider resolves the SAME policy instance
/// from the DI container and therefore shares the same concurrency permit pool.
/// AnthropicSynchronous wraps the SAME Anthropic bulkhead instance (not a second one)
/// with an additional outer Timeout — the synchronous RegulatoryScoreController path
/// intentionally competes for the same Anthropic quota as background jobs; it just
/// fails fast instead of hanging if that quota is saturated.
/// </summary>
public class ProviderBulkheadPolicies
{
    public IAsyncPolicy<HttpResponseMessage> Anthropic { get; }
    public IAsyncPolicy<HttpResponseMessage> AnthropicSynchronous { get; }
    public IAsyncPolicy<HttpResponseMessage> DeepL { get; }
    public IAsyncPolicy<HttpResponseMessage> Gemini { get; }

    public ProviderBulkheadPolicies(IOptions<ProviderConcurrencyOptions> options)
    {
        var config = options.Value;

        Anthropic = ResiliencePolicies.GetProviderBulkheadPolicy(
            config.Anthropic.MaxConcurrency, config.Anthropic.MaxQueued);
        AnthropicSynchronous = ResiliencePolicies.GetProviderBulkheadWithTimeoutPolicy(
            Anthropic, config.Anthropic.SynchronousTimeoutSeconds ?? 30);

        DeepL = ResiliencePolicies.GetProviderBulkheadPolicy(
            config.DeepL.MaxConcurrency, config.DeepL.MaxQueued);
        Gemini = ResiliencePolicies.GetProviderBulkheadPolicy(
            config.Gemini.MaxConcurrency, config.Gemini.MaxQueued);
    }
}
