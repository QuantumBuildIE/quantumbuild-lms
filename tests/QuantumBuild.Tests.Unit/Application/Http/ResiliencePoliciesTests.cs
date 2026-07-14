using System.Diagnostics;
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Timeout;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Core.Application.Http;
using Xunit;

namespace QuantumBuild.Tests.Unit.Application.Http;

/// <summary>
/// Unit tests for the Bulkhead (concurrency-limiting) policy factory methods added to
/// ResiliencePolicies for provider-concurrency throttling (Chunk B), plus the
/// ProviderBulkheadPolicies DI singleton that ensures every HttpClient registration for a
/// given provider shares ONE permit pool rather than getting its own.
/// </summary>
public class ResiliencePoliciesTests
{
    [Fact]
    public async Task GetProviderBulkheadPolicy_PermitsUpToMaxConcurrency_ExcessCallsDoNotExecute()
    {
        // Arrange: bulkhead allows 2 concurrent executions, plenty of queue room for the rest
        var policy = ResiliencePolicies.GetProviderBulkheadPolicy(maxConcurrency: 2, maxQueuingActions: 10);

        var currentlyExecuting = 0;
        var maxObservedConcurrency = 0;
        var lockObj = new object();
        var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<HttpResponseMessage> RunOperation() => policy.ExecuteAsync(async () =>
        {
            lock (lockObj)
            {
                currentlyExecuting++;
                if (currentlyExecuting > maxObservedConcurrency)
                    maxObservedConcurrency = currentlyExecuting;
            }

            await releaseGate.Task; // hold the permit open until the test releases it

            lock (lockObj) { currentlyExecuting--; }
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Act: launch 5 concurrent operations against a bulkhead sized for 2
        var tasks = Enumerable.Range(0, 5).Select(_ => Task.Run(RunOperation)).ToArray();

        await WaitUntilAsync(() => Volatile.Read(ref maxObservedConcurrency) == 2, TimeSpan.FromSeconds(5));

        // Assert: never more than 2 executed concurrently even though 5 were launched —
        // the other 3 are parked in the bulkhead's queue, not inside the gated section
        lock (lockObj)
        {
            maxObservedConcurrency.Should().Be(2);
            currentlyExecuting.Should().Be(2);
        }

        releaseGate.SetResult();
        var results = await Task.WhenAll(tasks);
        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetProviderBulkheadPolicy_NPlusOnethCall_QueuesRatherThanExecutingImmediately()
    {
        // Arrange: bulkhead of size 1 — a strict single-slot queue
        var policy = ResiliencePolicies.GetProviderBulkheadPolicy(maxConcurrency: 1, maxQueuingActions: 5);

        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = false;

        var firstTask = Task.Run(() => policy.ExecuteAsync(async () =>
        {
            firstStarted.SetResult();
            await releaseFirst.Task;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await firstStarted.Task; // first call now holds the sole permit

        var secondTask = Task.Run(() => policy.ExecuteAsync(async () =>
        {
            secondStarted = true;
            await Task.Yield();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        // Give the second call ample time to (incorrectly) start if the bulkhead weren't limiting it
        await Task.Delay(300);
        secondStarted.Should().BeFalse(
            "the second call must be queued behind the saturated bulkhead, not dispatched immediately");

        releaseFirst.SetResult();
        await Task.WhenAll(firstTask, secondTask);
        secondStarted.Should().BeTrue("the second call should run once the first releases its permit");
    }

    [Fact]
    public async Task GetProviderBulkheadPolicy_ReleasesPermit_OnBothSuccessAndException()
    {
        // Arrange: a single-slot bulkhead so a leaked permit would deadlock the next call
        var policy = ResiliencePolicies.GetProviderBulkheadPolicy(maxConcurrency: 1, maxQueuingActions: 5);

        // A successful call acquires and releases the sole permit
        var ok = await policy.ExecuteAsync(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);

        // A failing call must ALSO release its permit — not leak it
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            policy.ExecuteAsync(() => throw new InvalidOperationException("boom")));

        // Prove the permit was released: the next call must complete promptly.
        // Wrapped in Task.WhenAny with a timeout so a leaked permit fails the test
        // instead of hanging the test run indefinitely.
        var thirdCallTask = policy.ExecuteAsync(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted)));
        var winner = await Task.WhenAny(thirdCallTask, Task.Delay(TimeSpan.FromSeconds(3)));

        winner.Should().Be(thirdCallTask, "the permit held by the failed call must have been released, not leaked");
        (await thirdCallTask).StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task GetProviderBulkheadWithTimeoutPolicy_ThrowsTimeoutRejected_WhenBulkheadStaysSaturated()
    {
        // Arrange: shared bulkhead of size 1, wrapped with a 1-second outer Timeout —
        // mirrors ProviderBulkheadPolicies.AnthropicSynchronous composing over the shared
        // Anthropic bulkhead for the RegulatoryScoreController synchronous request path.
        var sharedBulkhead = ResiliencePolicies.GetProviderBulkheadPolicy(maxConcurrency: 1, maxQueuingActions: 5);
        var timeoutWrapped = ResiliencePolicies.GetProviderBulkheadWithTimeoutPolicy(sharedBulkhead, timeoutSeconds: 1);

        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holdGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Saturate the sole permit via the plain (non-timeout) bulkhead instance — this is the
        // background-job-style caller that holds the permit indefinitely for this test.
        var firstTask = Task.Run(() => sharedBulkhead.ExecuteAsync(async () =>
        {
            firstStarted.SetResult();
            await holdGate.Task;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await firstStarted.Task; // bulkhead now saturated

        // Act: a synchronous-style caller queues behind the saturated bulkhead via the
        // timeout-wrapped policy — it must fail fast rather than hang indefinitely.
        Func<Task> secondCall = () => timeoutWrapped.ExecuteAsync(
            async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            },
            CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        await secondCall.Should().ThrowAsync<TimeoutRejectedException>();
        stopwatch.Stop();

        // Should fire close to the configured 1s timeout, not hang indefinitely
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));

        holdGate.SetResult();
        await firstTask; // cleanup — release the held permit
    }

    [Fact]
    public async Task ProviderBulkheadPolicies_SharesOnePermitPoolAcrossIndependentConsumers()
    {
        // Arrange: two independently-DI-resolved "consumer" classes standing in for two real
        // typed-HttpClient services (e.g. ClaudeHaikuBackTranslationService and
        // ClaudeSonnetBackTranslationService) that each inject ProviderBulkheadPolicies and use
        // .Anthropic. If ProviderBulkheadPolicies were mistakenly registered non-singleton, or if
        // each consumer built its own bulkhead, this test would fail — consumer B would run
        // concurrently with consumer A instead of queuing behind it.
        var services = new ServiceCollection();
        services.AddOptions<ProviderConcurrencyOptions>()
            .Configure(o =>
            {
                o.Anthropic = new ProviderConcurrencyLimits { MaxConcurrency = 1, MaxQueued = 10, SynchronousTimeoutSeconds = 30 };
                o.DeepL = new ProviderConcurrencyLimits { MaxConcurrency = 5, MaxQueued = 10 };
                o.Gemini = new ProviderConcurrencyLimits { MaxConcurrency = 5, MaxQueued = 10 };
            });
        services.AddSingleton<ProviderBulkheadPolicies>();
        services.AddTransient<DummyAnthropicConsumerA>();
        services.AddTransient<DummyAnthropicConsumerB>();

        await using var provider = services.BuildServiceProvider();

        var consumerA = provider.GetRequiredService<DummyAnthropicConsumerA>();
        var consumerB = provider.GetRequiredService<DummyAnthropicConsumerB>();

        var aStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bStarted = false;

        // Act: both consumers issue a "call" concurrently via real Task.Run execution
        // (not sequential awaits) so a race condition in permit sharing would actually surface.
        var taskA = Task.Run(() => consumerA.CallAsync(async () =>
        {
            aStarted.SetResult();
            await releaseA.Task;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await aStarted.Task; // consumer A now holds the sole shared Anthropic permit

        var taskB = Task.Run(() => consumerB.CallAsync(async () =>
        {
            bStarted = true;
            await Task.Yield();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        await Task.Delay(300);

        // Assert: consumer B must NOT have started — it shares consumer A's single permit,
        // it does not get an independent one from a separately-built bulkhead instance.
        bStarted.Should().BeFalse(
            "consumer B must share consumer A's single Anthropic permit pool, not receive its own");

        releaseA.SetResult();
        await Task.WhenAll(taskA, taskB);
        bStarted.Should().BeTrue("consumer B should run once consumer A releases the shared permit");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.Elapsed > timeout)
                throw new TimeoutException("Condition was not met within the allotted timeout.");
            await Task.Delay(10);
        }
    }

    /// <summary>Stand-in for a real Claude-calling typed-client service (e.g. ClaudeHaikuBackTranslationService).</summary>
    private sealed class DummyAnthropicConsumerA
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;
        public DummyAnthropicConsumerA(ProviderBulkheadPolicies policies) => _policy = policies.Anthropic;
        public Task<HttpResponseMessage> CallAsync(Func<Task<HttpResponseMessage>> operation) => _policy.ExecuteAsync(operation);
    }

    /// <summary>Stand-in for a second, independently-registered Claude-calling typed-client service (e.g. ClaudeSonnetBackTranslationService).</summary>
    private sealed class DummyAnthropicConsumerB
    {
        private readonly IAsyncPolicy<HttpResponseMessage> _policy;
        public DummyAnthropicConsumerB(ProviderBulkheadPolicies policies) => _policy = policies.Anthropic;
        public Task<HttpResponseMessage> CallAsync(Func<Task<HttpResponseMessage>> operation) => _policy.ExecuteAsync(operation);
    }
}
