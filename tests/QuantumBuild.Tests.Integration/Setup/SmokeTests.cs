using System.Net;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Tests.Common.TestTenant;
using QuantumBuild.Tests.Integration.Fixtures;

namespace QuantumBuild.Tests.Integration.Setup;

/// <summary>
/// Smoke tests to verify the integration test setup is working correctly.
/// These tests verify:
/// - The application starts correctly
/// - Authentication works as expected
/// - Database connection is functional
/// - Test tenant data is seeded correctly
///
/// NOTE: Tests should NEVER query QUANTUMBUILD tenant data - only test tenant data.
/// </summary>
public class SmokeTests : IntegrationTestBase
{
    // Use the test tenant ID (tests should ONLY use test tenant data)
    private static readonly Guid TestTenantId = TestTenantConstants.TenantId;

    public SmokeTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ApiEndpoint_ReturnsNotFoundForInvalidRoute()
    {
        // Act - Test that the API returns 404 for non-existent routes (basic routing test)
        var response = await UnauthenticatedClient.GetAsync("/api/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithValidAdminToken_Succeeds()
    {
        // Act
        var response = await AdminClient.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AuthenticatedRequest_WithoutToken_ReturnsUnauthorized()
    {
        // Act
        var response = await UnauthenticatedClient.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DatabaseConnection_Works()
    {
        // Act - This implicitly tests database connectivity
        var context = GetDbContext();
        var canConnect = await context.Database.CanConnectAsync();

        // Assert
        canConnect.Should().BeTrue("Database connection should work");
    }

    [Fact]
    public async Task TestTenant_IsSeeded()
    {
        // Arrange
        var context = GetDbContext();

        // Act - Check if test tenant exists (seeded by TestTenantSeeder)
        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == TestTenantId);

        // Assert
        tenant.Should().NotBeNull("Test tenant should be seeded");
        tenant!.Name.Should().Be(TestTenantConstants.TenantName);
    }

    [Fact]
    public async Task TestTenant_Sites_AreSeeded()
    {
        // Arrange
        var context = GetDbContext();

        // Act - Check if test tenant sites exist (seeded by TestTenantSeeder)
        var sites = await context.Sites
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == TestTenantId)
            .ToListAsync();

        // Assert
        sites.Should().NotBeEmpty("At least one test site should be seeded");
        sites.Should().Contain(s => s.Id == TestTenantConstants.Sites.MainSite);
    }

    [Fact]
    public async Task CreateAuthenticatedClient_WithCustomPermissions_Works()
    {
        // Arrange - Create a client with specific permissions
        var customClient = Factory.CreateAuthenticatedClient(
            userId: Guid.NewGuid(),
            email: "custom@test.com",
            roles: new[] { "Custom" },
            permissions: new[] { "ToolboxTalks.View" }
        );

        // Act - Should be able to view toolbox talks
        var response = await customClient.GetAsync("/api/toolbox-talks");

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue($"Custom client should have access with correct permissions, got {response.StatusCode}");

        customClient.Dispose();
    }

    [Fact]
    public void FakeEmailSender_CapturesEmails()
    {
        // Arrange
        var emailSender = Factory.FakeEmailSender;
        emailSender.Clear();

        // Act
        emailSender.SendEmailAsync("test@example.com", "Test Subject", "Test Body");

        // Assert
        emailSender.Count.Should().Be(1);
        emailSender.LastEmail.Should().NotBeNull();
        emailSender.LastEmail!.To.Should().Be("test@example.com");
        emailSender.LastEmail.Subject.Should().Be("Test Subject");
    }
}
