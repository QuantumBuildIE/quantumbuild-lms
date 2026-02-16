namespace QuantumBuild.Tests.Integration.Api;

/// <summary>
/// Basic tests to verify the API is running and responding.
/// </summary>
public class HealthCheckTests : IntegrationTestBase
{
    public HealthCheckTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task SwaggerEndpoint_ReturnsExpectedStatus()
    {
        // Act
        var response = await UnauthenticatedClient.GetAsync("/swagger/index.html");

        // Assert - Swagger may be disabled in test environment, so accept OK or NotFound
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthEndpoint_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Act
        var response = await UnauthenticatedClient.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthEndpoint_ReturnsSuccess_WhenAuthenticated()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
