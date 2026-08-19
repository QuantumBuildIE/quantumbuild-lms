namespace QuantumBuild.Tests.Integration.Fixtures;

/// <summary>
/// Opt-in collection for full-flow tests that need to prove a job enqueued via
/// <c>BackgroundJob.Enqueue&lt;T&gt;</c> is actually dispatched and executed by a real Hangfire server
/// (<see cref="HangfireDispatchWebApplicationFactory"/>), as opposed to the default "Integration"
/// collection's <see cref="CustomWebApplicationFactory"/>, which deliberately never runs a Hangfire
/// server (see the comment on that class).
///
/// HOW TO USE (reusable pattern for later slices):
///
/// <code>
/// [Collection("HangfireDispatch")]
/// public class MyRealDispatchTests : IntegrationTestBase
/// {
///     public MyRealDispatchTests(HangfireDispatchWebApplicationFactory factory) : base(factory) { }
///
///     [Fact]
///     public async Task MyJob_RealDispatch_DoesTheThing()
///     {
///         // ... seed via AdminClient/API as usual ...
///
///         await HangfireDispatchTestHelpers.EnqueueAndAwaitSuccessAsync&lt;MyJob&gt;(
///             job =&gt; job.ExecuteAsync(CancellationToken.None));
///
///         // ... assert DB side effects as usual ...
///     }
/// }
/// </code>
///
/// A test class re-declares <c>[Collection("HangfireDispatch")]</c> itself (overriding the
/// <c>[Collection("Integration")]</c> declared on <see cref="IntegrationTestBase"/>) — this mirrors the
/// existing codebase convention of every "Integration" test class re-declaring its own
/// <c>[Collection("Integration")]</c> rather than relying purely on the base class's attribute. See
/// <see cref="QuantumBuild.Tests.Integration.ToolboxTalks.RecurringScheduleRealDispatchTests"/> for the reference example, and
/// <see cref="HangfireDispatchTestHelpers"/> for the enqueue-and-poll helper used inside the test body.
/// </summary>
[CollectionDefinition("HangfireDispatch")]
public class HangfireDispatchTestCollection : ICollectionFixture<HangfireDispatchWebApplicationFactory>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition] and
    // ICollectionFixture<HangfireDispatchWebApplicationFactory>.
}
