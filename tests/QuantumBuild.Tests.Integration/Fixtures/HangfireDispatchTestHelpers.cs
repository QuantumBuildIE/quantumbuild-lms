using System.Linq.Expressions;
using Hangfire;
using Hangfire.Storage.Monitoring;

namespace QuantumBuild.Tests.Integration.Fixtures;

/// <summary>
/// Reusable enqueue-and-poll utility for full-flow tests that need to prove a job is actually
/// dispatched and executed by a real Hangfire server, not just that its <c>ExecuteAsync</c> logic is
/// correct in isolation (the existing <c>ActivatorUtilities.CreateInstance</c> + direct call pattern —
/// see RecurringScheduleRefreshCharacterisationTests — already covers that, and remains the right choice
/// when dispatch itself isn't what's under test).
///
/// Only usable from a test class collected under <c>[Collection("HangfireDispatch")]</c> (see
/// <see cref="HangfireDispatchTestCollection"/>), which supplies a
/// <see cref="HangfireDispatchWebApplicationFactory"/> — the only factory in this project with a real
/// Hangfire server enabled. Using this helper from the default "Integration" collection will time out,
/// because nothing dequeues the job there by design.
/// </summary>
public static class HangfireDispatchTestHelpers
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Enqueues <paramref name="methodCall"/> on the real Hangfire queue — the same
    /// <c>BackgroundJob.Enqueue&lt;T&gt;</c> call production code paths use (see CLAUDE.md Note 21) —
    /// then polls Hangfire's own monitoring API until the real, DI-resolved worker reports the job
    /// Succeeded. This is the real completion signal (the job's own state history), not a fixed sleep.
    /// </summary>
    /// <exception cref="InvalidOperationException">The job reached the Failed or Deleted state.</exception>
    /// <exception cref="TimeoutException">The job did not reach a terminal state within <paramref name="timeout"/>.</exception>
    /// <returns>The Hangfire job id, in case the caller wants to inspect it further.</returns>
    public static async Task<string> EnqueueAndAwaitSuccessAsync<TJob>(
        Expression<Func<TJob, Task>> methodCall,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null)
    {
        var jobId = BackgroundJob.Enqueue(methodCall);
        await AwaitSuccessAsync(jobId, timeout, pollInterval);
        return jobId;
    }

    /// <summary>
    /// Polls Hangfire's monitoring API for an already-enqueued job id until it reports Succeeded.
    /// </summary>
    public static async Task AwaitSuccessAsync(string jobId, TimeSpan? timeout = null, TimeSpan? pollInterval = null)
    {
        var effectiveTimeout = timeout ?? DefaultTimeout;
        var interval = pollInterval ?? DefaultPollInterval;
        var deadline = DateTime.UtcNow + effectiveTimeout;
        var monitoringApi = JobStorage.Current.GetMonitoringApi();

        while (true)
        {
            var details = monitoringApi.JobDetails(jobId);
            var history = details?.History ?? new List<StateHistoryDto>();

            if (history.Any(h => h.StateName == "Succeeded"))
            {
                return;
            }

            var failed = history.FirstOrDefault(h => h.StateName == "Failed");
            if (failed != null)
            {
                throw new InvalidOperationException(
                    $"Hangfire job {jobId} reached the Failed state: {DescribeFailure(failed)}");
            }

            if (history.Any(h => h.StateName == "Deleted"))
            {
                throw new InvalidOperationException($"Hangfire job {jobId} was deleted before completing.");
            }

            if (DateTime.UtcNow >= deadline)
            {
                var statesObserved = history.Count == 0 ? "(none — job not found)" : string.Join(", ", history.Select(h => h.StateName));
                throw new TimeoutException(
                    $"Hangfire job {jobId} did not reach the Succeeded state within {effectiveTimeout}. " +
                    $"States observed: [{statesObserved}]. Is AddHangfireServer() enabled for this " +
                    "factory (HangfireDispatchWebApplicationFactory), and is the test class collected " +
                    "under [Collection(\"HangfireDispatch\")]?");
            }

            await Task.Delay(interval);
        }
    }

    private static string DescribeFailure(StateHistoryDto failedState)
    {
        if (failedState.Data == null || failedState.Data.Count == 0)
        {
            return "(no failure details captured)";
        }

        failedState.Data.TryGetValue("ExceptionType", out var exceptionType);
        failedState.Data.TryGetValue("ExceptionMessage", out var exceptionMessage);
        return $"{exceptionType}: {exceptionMessage}";
    }
}
