// Test collections in this project run one at a time. Historically this made no practical difference,
// since every existing test class shared the single "Integration" collection (see
// Fixtures/IntegrationTestCollection.cs) and so was already fully sequential.
//
// Adding the "HangfireDispatch" collection (see Fixtures/HangfireDispatchTestCollection.cs) changes
// that: HangfireDispatchWebApplicationFactory enables a real Hangfire server against Hangfire's own
// process-global static storage configuration (GlobalConfiguration.Configuration / JobStorage.Current).
// Without disabling cross-collection parallelisation, xUnit could run "HangfireDispatch" tests
// concurrently with "Integration" tests, letting one collection's Hangfire server dequeue and execute
// jobs meant for the other collection's storage. Disabling parallelisation removes that race at zero
// cost to the existing suite's wall-clock time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
