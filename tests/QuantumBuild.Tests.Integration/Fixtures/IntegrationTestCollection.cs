namespace QuantumBuild.Tests.Integration.Fixtures;

/// <summary>
/// Defines a collection of integration tests that share a single CustomWebApplicationFactory instance.
/// This ensures that the database container is only created once per test run, significantly
/// improving test execution time.
///
/// All test classes that should share the same factory should use [Collection("Integration")].
/// </summary>
/// <remarks>
/// This class has no code - it serves as a marker for xUnit to identify the collection.
/// The ICollectionFixture interface tells xUnit to create a single instance of
/// CustomWebApplicationFactory and share it across all test classes in this collection.
/// </remarks>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    // This class has no code, and is never created.
    // Its purpose is to be the place to apply [CollectionDefinition] and
    // all the ICollectionFixture<> interfaces.
}
