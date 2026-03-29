using System.Net;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace QuantumBuild.Core.Application.Http;

public static class ResiliencePolicies
{
    private static readonly HttpStatusCode[] ClaudeRetryStatusCodes =
    [
        HttpStatusCode.TooManyRequests,        // 429
        HttpStatusCode.InternalServerError,    // 500
        HttpStatusCode.BadGateway,             // 502
        HttpStatusCode.ServiceUnavailable,     // 503
        (HttpStatusCode)529                    // Overloaded (Anthropic-specific)
    ];

    private static readonly Random Jitter = new();

    /// <summary>
    /// Claude API: 3 retries, exponential backoff 2s/4s/8s with ±500ms jitter.
    /// Triggers on HttpRequestException and status codes 429, 500, 502, 503, 529.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetClaudePolicy(ILogger logger)
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => ClaudeRetryStatusCodes.Contains(r.StatusCode))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: (attempt, _, _) =>
                {
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s, 8s
                    var jitter = TimeSpan.FromMilliseconds(Jitter.Next(-500, 501));
                    return baseDelay + jitter;
                },
                onRetryAsync: (outcome, delay, attempt, _) =>
                {
                    var reason = outcome.Exception is not null
                        ? $"Exception: {outcome.Exception.GetType().Name}"
                        : $"StatusCode: {(int)outcome.Result.StatusCode}";

                    logger.LogWarning(
                        "Claude API retry {Attempt}/3 after {Delay}ms — {Reason}",
                        attempt, delay.TotalMilliseconds, reason);

                    return Task.CompletedTask;
                });
    }

    /// <summary>
    /// ElevenLabs API: 2 retries, exponential backoff 2s/4s with ±500ms jitter.
    /// Fewer retries because large audio uploads are expensive to repeat.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetElevenLabsPolicy(ILogger logger)
    {
        return Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(r => ClaudeRetryStatusCodes.Contains(r.StatusCode))
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: (attempt, _, _) =>
                {
                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s
                    var jitter = TimeSpan.FromMilliseconds(Jitter.Next(-500, 501));
                    return baseDelay + jitter;
                },
                onRetryAsync: (outcome, delay, attempt, _) =>
                {
                    var reason = outcome.Exception is not null
                        ? $"Exception: {outcome.Exception.GetType().Name}"
                        : $"StatusCode: {(int)outcome.Result.StatusCode}";

                    logger.LogWarning(
                        "ElevenLabs API retry {Attempt}/2 after {Delay}ms — {Reason}",
                        attempt, delay.TotalMilliseconds, reason);

                    return Task.CompletedTask;
                });
    }

    /// <summary>
    /// Generic transient policy: 3 retries, exponential backoff 1s/2s/4s.
    /// For DeepL, Gemini, DeepSeek and other external providers.
    /// Triggers on HttpRequestException, 429, and all 5xx status codes.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> GetTransientPolicy(ILogger logger, string providerName = "External")
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // HttpRequestException + 5xx + 408
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests) // 429
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), // 1s, 2s, 4s
                onRetryAsync: (outcome, delay, attempt, _) =>
                {
                    var reason = outcome.Exception is not null
                        ? $"Exception: {outcome.Exception.GetType().Name}"
                        : $"StatusCode: {(int)outcome.Result.StatusCode}";

                    logger.LogWarning(
                        "{Provider} API retry {Attempt}/3 after {Delay}ms — {Reason}",
                        providerName, attempt, delay.TotalMilliseconds, reason);

                    return Task.CompletedTask;
                });
    }
}
