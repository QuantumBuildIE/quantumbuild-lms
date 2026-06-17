using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Domain.Entities;

namespace QuantumBuild.Tests.Integration.Infrastructure;

[Collection("Integration")]
public class SeederBehaviourTests : IntegrationTestBase
{
    public SeederBehaviourTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task DataSeeder_InNonDevelopmentEnvironment_DoesNotCreateSuperUserAccount()
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = await userManager.FindByEmailAsync("superuser@certifiediq.ai");

        user.Should().BeNull("DataSeeder should only create credentialled accounts in Development");
    }

    [Fact]
    public async Task DataSeeder_InNonDevelopmentEnvironment_DoesNotCreateAdminAccount()
    {
        using var scope = Factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = await userManager.FindByEmailAsync("admin@quantumbuild.ai");

        user.Should().BeNull("DataSeeder should only create credentialled accounts in Development");
    }
}
