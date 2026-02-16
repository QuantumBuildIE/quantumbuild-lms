using System.Net;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Tests.Common.TestTenant;

namespace QuantumBuild.Tests.Integration.Fixtures;

/// <summary>
/// Base class for integration tests providing common setup and utilities.
/// Uses ICollectionFixture for shared factory across test classes.
/// </summary>
[Collection("Integration")]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected IServiceScope? Scope;

    // Pre-configured authenticated clients for different user types
    protected HttpClient AdminClient { get; private set; } = null!;
    protected HttpClient SiteManagerClient { get; private set; } = null!;
    protected HttpClient WarehouseClient { get; private set; } = null!;
    protected HttpClient OperatorClient { get; private set; } = null!;
    protected HttpClient FinanceClient { get; private set; } = null!;
    protected HttpClient UnauthenticatedClient { get; private set; } = null!;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
    }

    public virtual async Task InitializeAsync()
    {
        // Reset database to clean state before each test class
        // This ensures test isolation - data from previous test classes doesn't interfere
        await Factory.ResetDatabaseAsync();

        // Initialize authenticated clients
        AdminClient = Factory.CreateAuthenticatedClient(TestUserType.Admin);
        SiteManagerClient = Factory.CreateAuthenticatedClient(TestUserType.SiteManager);
        WarehouseClient = Factory.CreateAuthenticatedClient(TestUserType.Warehouse);
        OperatorClient = Factory.CreateAuthenticatedClient(TestUserType.Operator);
        FinanceClient = Factory.CreateAuthenticatedClient(TestUserType.Finance);
        UnauthenticatedClient = Factory.CreateClient();
    }

    public virtual async Task DisposeAsync()
    {
        Scope?.Dispose();
        AdminClient?.Dispose();
        SiteManagerClient?.Dispose();
        WarehouseClient?.Dispose();
        OperatorClient?.Dispose();
        FinanceClient?.Dispose();
        UnauthenticatedClient?.Dispose();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets a scoped service from the factory's service provider.
    /// </summary>
    protected T GetService<T>() where T : notnull
    {
        Scope ??= Factory.Services.CreateScope();
        return Scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets the application DbContext for database operations.
    /// </summary>
    protected ApplicationDbContext GetDbContext()
    {
        Scope = Factory.Services.CreateScope();
        return Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    #region Fake Subtitle Services

    /// <summary>
    /// Gets the fake transcription service for test configuration.
    /// </summary>
    protected FakeTranscriptionService FakeTranscriptionService => Factory.FakeTranscriptionService;

    /// <summary>
    /// Gets the fake translation service for test configuration.
    /// </summary>
    protected FakeTranslationService FakeTranslationService => Factory.FakeTranslationService;

    /// <summary>
    /// Gets the fake SRT storage provider for test configuration.
    /// </summary>
    protected FakeSrtStorageProvider FakeSrtStorageProvider => Factory.FakeSrtStorageProvider;

    /// <summary>
    /// Gets the fake video source provider for test configuration.
    /// </summary>
    protected FakeVideoSourceProvider FakeVideoSourceProvider => Factory.FakeVideoSourceProvider;

    /// <summary>
    /// Gets the fake subtitle progress reporter for test configuration.
    /// </summary>
    protected FakeSubtitleProgressReporter FakeSubtitleProgressReporter => Factory.FakeSubtitleProgressReporter;

    /// <summary>
    /// Resets all fake subtitle services to their default state.
    /// Call this in InitializeAsync or at the beginning of tests that need fresh fake services.
    /// </summary>
    protected void ResetFakeSubtitleServices()
    {
        FakeTranscriptionService.Reset();
        FakeTranslationService.Reset();
        FakeSrtStorageProvider.Reset();
        FakeVideoSourceProvider.Reset();
        FakeSubtitleProgressReporter.Clear();
    }

    #endregion

    /// <summary>
    /// Resets the database to a clean state and re-seeds test data.
    /// Call this at the start of tests that modify data.
    /// </summary>
    protected async Task ResetDatabaseAsync()
    {
        await Factory.ResetDatabaseAsync();
    }

    /// <summary>
    /// Seeds the test tenant data into the database.
    /// Uses UserManager for proper password hashing so test users can login via the auth endpoint.
    /// </summary>
    protected async Task SeedTestTenantAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestTenantSeeder>>();

        // Pass the service provider so TestTenantSeeder can use UserManager for password hashing
        var seeder = new TestTenantSeeder(context, logger, scope.ServiceProvider);
        await seeder.SeedAllAsync();
    }

    /// <summary>
    /// Cleans up test tenant data from the database.
    /// </summary>
    protected async Task CleanupTestTenantAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TestTenantSeeder>>();

        var seeder = new TestTenantSeeder(context, logger);
        await seeder.CleanupAsync();
    }

    #region HTTP Helper Methods

    /// <summary>
    /// Performs a GET request and deserializes the response.
    /// </summary>
    protected async Task<T?> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    /// <summary>
    /// Performs a GET request and returns the response.
    /// </summary>
    protected async Task<HttpResponseMessage> GetAsync(HttpClient client, string url)
    {
        return await client.GetAsync(url);
    }

    /// <summary>
    /// Performs a POST request with JSON body.
    /// </summary>
    protected async Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string url, T data)
    {
        return await client.PostAsJsonAsync(url, data);
    }

    /// <summary>
    /// Performs a POST request with JSON body and deserializes the response.
    /// </summary>
    protected async Task<TResponse?> PostAndReadAsync<TRequest, TResponse>(HttpClient client, string url, TRequest data)
    {
        var response = await client.PostAsJsonAsync(url, data);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    /// <summary>
    /// Performs a PUT request with JSON body.
    /// </summary>
    protected async Task<HttpResponseMessage> PutAsync<T>(HttpClient client, string url, T data)
    {
        return await client.PutAsJsonAsync(url, data);
    }

    /// <summary>
    /// Performs a PUT request with JSON body and deserializes the response.
    /// </summary>
    protected async Task<TResponse?> PutAndReadAsync<TRequest, TResponse>(HttpClient client, string url, TRequest data)
    {
        var response = await client.PutAsJsonAsync(url, data);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>();
    }

    /// <summary>
    /// Performs a DELETE request.
    /// </summary>
    protected async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string url)
    {
        return await client.DeleteAsync(url);
    }

    #endregion

    #region Assertion Helpers

    /// <summary>
    /// Asserts that an unauthenticated request returns 401 Unauthorized.
    /// </summary>
    protected async Task AssertUnauthorizedAsync(string url, HttpMethod method = null!)
    {
        method ??= HttpMethod.Get;
        var request = new HttpRequestMessage(method, url);
        var response = await UnauthenticatedClient.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Asserts that a request without the required permission returns 403 Forbidden.
    /// </summary>
    protected async Task AssertForbiddenAsync(HttpClient client, string url, HttpMethod method = null!)
    {
        method ??= HttpMethod.Get;
        var request = new HttpRequestMessage(method, url);
        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Asserts that a request returns a successful status code (2xx).
    /// </summary>
    protected async Task AssertSuccessAsync(HttpClient client, string url, HttpMethod method = null!)
    {
        method ??= HttpMethod.Get;
        var request = new HttpRequestMessage(method, url);
        var response = await client.SendAsync(request);
        response.IsSuccessStatusCode.Should().BeTrue($"Expected success but got {response.StatusCode}");
    }

    #endregion

    #region Authentication Helpers

    /// <summary>
    /// Authenticates as the test admin user and returns an authenticated client.
    /// Uses the actual login endpoint (for testing login functionality).
    /// </summary>
    protected async Task<HttpClient> GetLoginAuthenticatedClientAsync(
        string email = "admin@test.quantumbuild.ie",
        string password = "TestAdmin123!")
    {
        var token = await GetLoginAuthTokenAsync(email, password);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>
    /// Gets an auth token by calling the actual login endpoint.
    /// </summary>
    private async Task<string> GetLoginAuthTokenAsync(string email, string password)
    {
        var loginRequest = new
        {
            email,
            password
        };

        var response = await UnauthenticatedClient.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return result?.AccessToken ?? throw new InvalidOperationException("Failed to get auth token");
    }

    private record LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);

    #endregion
}
