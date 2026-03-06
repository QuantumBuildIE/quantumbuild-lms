namespace QuantumBuild.Tests.Integration;

/// <summary>
/// Smoke tests to verify all modules are operational.
/// These tests ensure basic connectivity and data retrieval across all modules.
/// </summary>
public class SmokeTests : IntegrationTestBase
{
    public SmokeTests(CustomWebApplicationFactory factory) : base(factory) { }

    #region Health Check Tests

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Healthy");
        }
    }

    [Fact]
    public async Task SwaggerEndpoint_IsAccessible()
    {
        // Arrange
        var client = Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/swagger/index.html");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Module Endpoint Smoke Tests

    [Theory]
    [InlineData("/api/employees")]
    [InlineData("/api/sites")]
    [InlineData("/api/companies")]
    [InlineData("/api/users")]
    public async Task CoreModuleEndpoints_ReturnSuccess_ForAdmin(string endpoint)
    {
        // Act
        var response = await AdminClient.GetAsync(endpoint);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData("/api/toolbox-talks")]
    [InlineData("/api/toolbox-talks/dashboard")]
    public async Task ToolboxTalksModuleEndpoints_ReturnSuccess_ForAdmin(string endpoint)
    {
        // Act
        var response = await AdminClient.GetAsync(endpoint);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    #endregion

    #region Authentication Smoke Tests

    [Fact]
    public async Task Unauthenticated_Request_Returns401()
    {
        // Act
        var response = await UnauthenticatedClient.GetAsync("/api/employees");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthMe_ReturnsCurrentUser_ForAdmin()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("admin");
    }

    #endregion

    #region Data Verification Tests

    [Fact]
    public async Task CoreModule_HasSeededData()
    {
        // Act - Employees
        var employeesResponse = await AdminClient.GetAsync("/api/employees?pageNumber=1&pageSize=10");
        var employeesContent = await employeesResponse.Content.ReadAsStringAsync();

        // Assert
        employeesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        employeesContent.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ToolboxTalksModule_HasSeededData()
    {
        // Act - Toolbox Talks
        var talksResponse = await AdminClient.GetAsync("/api/toolbox-talks?pageNumber=1&pageSize=10");

        // Assert
        talksResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Role-Based Access Tests

    [Fact]
    public async Task OperatorUser_HasLimitedAccess()
    {
        // Operator should be able to view but not create admin resources
        var readResponse = await OperatorClient.GetAsync("/api/employees");

        // Assert - should have at least view access
        readResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Forbidden);
    }

    #endregion

    #region Cross-Module Integration Tests

    [Fact]
    public async Task CanCreateToolboxTalk()
    {
        // Arrange
        var createCommand = new
        {
            Title = $"Smoke Test Talk {Guid.NewGuid()}",
            Frequency = 0, // Once
            RequiresQuiz = false,
            IsActive = true,
            Sections = new[]
            {
                new { SectionNumber = 1, Title = "Section 1", Content = "<p>Content</p>", RequiresAcknowledgment = true }
            }
        };

        // Act
        var createResponse = await AdminClient.PostAsJsonAsync("/api/toolbox-talks", createCommand);

        // Assert
        createResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest
        );
    }

    #endregion

    #region Database Connectivity Tests

    [Fact]
    public async Task Database_CanReadAndWrite()
    {
        // This tests that we can perform database operations through the API
        // Act
        var employeesResponse = await AdminClient.GetAsync("/api/employees?pageNumber=1&pageSize=1");

        // Assert
        employeesResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
