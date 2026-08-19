using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace QuantumBuild.Tests.Integration.Fixtures;

/// <summary>
/// Opt-in variant of <see cref="CustomWebApplicationFactory"/> that enables a REAL Hangfire server,
/// so a job enqueued via <c>BackgroundJob.Enqueue&lt;T&gt;</c> is actually dequeued and executed by a
/// worker, instead of sitting untouched in storage.
///
/// <see cref="CustomWebApplicationFactory"/> deliberately never calls <c>AddHangfireServer()</c> (see
/// the comment at CustomWebApplicationFactory.cs around the Hangfire re-registration block) because the
/// other 745+ integration tests only need a job's <c>ExecuteAsync</c> logic proven against a real
/// database, not Hangfire's own dispatch pipeline (enqueue -> storage -> worker pickup). This factory
/// closes that specific gap.
///
/// This factory owns its own Testcontainers Postgres instance and lives in its own xUnit collection
/// ("HangfireDispatch", see <see cref="HangfireDispatchTestCollection"/>) rather than sharing the
/// "Integration" collection's factory, for two reasons:
///
///  1. Hangfire's storage configuration (<c>GlobalConfiguration.Configuration</c> /
///     <c>JobStorage.Current</c>) is process-global static state. Running a real server against the
///     same storage the other 745 tests share would let this factory's worker pick up jobs enqueued by
///     unrelated tests, and vice versa.
///  2. It keeps the change strictly additive/opt-in — nothing about the default factory, its
///     database container, or its tests changes.
///
/// See AssemblyInfo.cs for the companion change that disables cross-collection test parallelisation,
/// which is what actually makes the process-global Hangfire state above safe to use here.
/// </summary>
public class HangfireDispatchWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // The base factory already re-registers Hangfire with in-memory storage but never adds
            // a server (see CustomWebApplicationFactory). Add one here so enqueued jobs are actually
            // picked up and run, exercising the real dispatch path.
            services.AddHangfireServer(options =>
            {
                options.Queues = new[] { "default", "content-generation" };
                options.WorkerCount = 1;
                options.SchedulePollingInterval = TimeSpan.FromMilliseconds(200);
            });
        });
    }
}
