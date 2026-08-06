using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Core.Infrastructure.Identity;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Standards;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.TestTenant;
using QuantumBuild.Tests.Integration.Setup;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Chunk 3 of the multi-standard regulatory feature: the tenant admin surface for subscribing
/// to Standard-kind RegulatoryBody entries via /api/tenants/{tenantId}/standards.
/// Note: Respawner only resets the "public" schema, not "toolbox_talks" (where RegulatoryBody,
/// Sector and TenantStandardSubscription live), so rows persist across test methods within a
/// run — every test uses unique codes/keys rather than relying on a clean table.
/// </summary>
[Collection("Integration")]
public class TenantStandardSubscriptionTests : IntegrationTestBase
{
    public TenantStandardSubscriptionTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UniqueSuffix(string label) => $"{label}-{Guid.NewGuid():N}"[..30];

    private async Task<Sector> CreateSectorAsync(string label)
    {
        var context = GetDbContext();
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Key = $"tss-{Guid.NewGuid():N}"[..20],
            Name = $"Standard Sub Test Sector {UniqueSuffix(label)}",
            DisplayOrder = 99,
            IsActive = true
        };
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();
        return sector;
    }

    private async Task AssignSectorToTenantAsync(Guid tenantId, Guid sectorId)
    {
        var context = GetDbContext();
        context.TenantSectors.Add(new TenantSector
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SectorId = sectorId,
            IsDefault = true
        });
        await context.SaveChangesAsync();
    }

    private async Task<RegulatoryBody> CreateStandardBodyAsync(string label, Guid sectorId)
    {
        var context = GetDbContext();
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = $"Standard Sub Test Body {UniqueSuffix(label)}",
            Code = $"STD{Guid.NewGuid():N}"[..15],
            Country = "International",
            Kind = RegulatoryBodyKind.Standard,
            SectorId = sectorId
        };
        context.RegulatoryBodies.Add(body);
        await context.SaveChangesAsync();
        return body;
    }

    private async Task<RegulatoryBody> CreateRegulationBodyAsync(string label)
    {
        var context = GetDbContext();
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = $"Standard Sub Test Regulation {UniqueSuffix(label)}",
            Code = $"REG{Guid.NewGuid():N}"[..15],
            Country = "IE",
            Kind = RegulatoryBodyKind.Regulation
        };
        context.RegulatoryBodies.Add(body);
        await context.SaveChangesAsync();
        return body;
    }

    private async Task EnsureTenantBExistsAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var exists = await db.Set<Tenant>()
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id == TestTenantConstants.TenantB.TenantId);

        if (exists) return;

        db.Set<Tenant>().Add(new Tenant
        {
            Id = TestTenantConstants.TenantB.TenantId,
            Name = TestTenantConstants.TenantB.TenantName,
            Code = TestTenantConstants.TenantB.TenantCode,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test-seeder",
        });
        await db.SaveChangesAsync();
    }

    private HttpClient CreateTenantBAdminClient() =>
        Factory.CreateAuthenticatedClient(
            TestTenantConstants.TenantB.Users.Admin.Id,
            TestTenantConstants.TenantB.Users.Admin.Email,
            TestTenantConstants.TenantB.TenantId,
            new[] { "Admin" },
            Permissions.GetAll());

    private static string AvailableUrl(bool includeCrossSector = false) =>
        $"/api/tenants/{TestTenantConstants.TenantId}/standards/available" +
        (includeCrossSector ? "?includeCrossSector=true" : "");

    private static string SubscribedUrl(Guid? tenantId = null) =>
        $"/api/tenants/{tenantId ?? TestTenantConstants.TenantId}/standards";

    private static string SubscribeUrl(Guid regulatoryBodyId, Guid? tenantId = null) =>
        $"/api/tenants/{tenantId ?? TestTenantConstants.TenantId}/standards/{regulatoryBodyId}";

    // ── GET available ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailable_ReturnsInSectorStandards_ForTenantsActiveSectors()
    {
        var sector = await CreateSectorAsync(nameof(GetAvailable_ReturnsInSectorStandards_ForTenantsActiveSectors));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var body = await CreateStandardBodyAsync(nameof(GetAvailable_ReturnsInSectorStandards_ForTenantsActiveSectors), sector.Id);

        var (response, available) = await AdminClient.GetWithResponseAsync<List<AvailableStandardDto>>(AvailableUrl());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = available!.Single(s => s.Id == body.Id);
        listed.SectorId.Should().Be(sector.Id);
        listed.IsCrossSector.Should().BeFalse();
        listed.IsSubscribed.Should().BeFalse();
    }

    [Fact]
    public async Task GetAvailable_ExcludesOutOfSectorStandards_UnlessIncludeCrossSectorRequested()
    {
        var outOfSectorSector = await CreateSectorAsync(nameof(GetAvailable_ExcludesOutOfSectorStandards_UnlessIncludeCrossSectorRequested));
        var outOfSectorBody = await CreateStandardBodyAsync(
            nameof(GetAvailable_ExcludesOutOfSectorStandards_UnlessIncludeCrossSectorRequested), outOfSectorSector.Id);

        var (defaultResponse, defaultAvailable) = await AdminClient.GetWithResponseAsync<List<AvailableStandardDto>>(AvailableUrl());
        defaultResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        defaultAvailable!.Select(s => s.Id).Should().NotContain(outOfSectorBody.Id);

        var (crossResponse, crossAvailable) = await AdminClient.GetWithResponseAsync<List<AvailableStandardDto>>(AvailableUrl(includeCrossSector: true));
        crossResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var crossListed = crossAvailable!.Single(s => s.Id == outOfSectorBody.Id);
        crossListed.IsCrossSector.Should().BeTrue();
    }

    [Fact]
    public async Task GetAvailable_MarksAlreadySubscribedStandards()
    {
        var sector = await CreateSectorAsync(nameof(GetAvailable_MarksAlreadySubscribedStandards));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var body = await CreateStandardBodyAsync(nameof(GetAvailable_MarksAlreadySubscribedStandards), sector.Id);

        var subscribeResponse = await AdminClient.PostAsync(SubscribeUrl(body.Id), null);
        subscribeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var (response, available) = await AdminClient.GetWithResponseAsync<List<AvailableStandardDto>>(AvailableUrl());
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        available!.Single(s => s.Id == body.Id).IsSubscribed.Should().BeTrue();
    }

    // ── GET subscribed ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetSubscribed_ReturnsCurrentSubscriptionsWithBodyDetails()
    {
        var sector = await CreateSectorAsync(nameof(GetSubscribed_ReturnsCurrentSubscriptionsWithBodyDetails));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var body = await CreateStandardBodyAsync(nameof(GetSubscribed_ReturnsCurrentSubscriptionsWithBodyDetails), sector.Id);

        var subscribeResponse = await AdminClient.PostAsync(SubscribeUrl(body.Id), null);
        subscribeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var (response, subscribed) = await AdminClient.GetWithResponseAsync<List<TenantStandardSubscriptionDto>>(SubscribedUrl());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = subscribed!.Single(s => s.RegulatoryBodyId == body.Id);
        listed.Name.Should().Be(body.Name);
        listed.Code.Should().Be(body.Code);
        listed.SectorId.Should().Be(sector.Id);
        listed.SectorName.Should().Be(sector.Name);
        listed.IsCrossSector.Should().BeFalse();
    }

    // ── POST subscribe ───────────────────────────────────────────────────────

    [Fact]
    public async Task Subscribe_CreatesSubscription()
    {
        var sector = await CreateSectorAsync(nameof(Subscribe_CreatesSubscription));
        var body = await CreateStandardBodyAsync(nameof(Subscribe_CreatesSubscription), sector.Id);

        var (response, dto) = await AdminClient.PostWithResponseAsync<object?, TenantStandardSubscriptionDto>(
            SubscribeUrl(body.Id), null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto!.RegulatoryBodyId.Should().Be(body.Id);
        dto.TenantId.Should().Be(TestTenantConstants.TenantId);

        var context = GetDbContext();
        var reloaded = await context.TenantStandardSubscriptions
            .IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == TestTenantConstants.TenantId && s.RegulatoryBodyId == body.Id);
        reloaded.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Subscribe_OnRegulationBody_Returns400()
    {
        var body = await CreateRegulationBodyAsync(nameof(Subscribe_OnRegulationBody_Returns400));

        var response = await AdminClient.PostAsync(SubscribeUrl(body.Id), null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Subscribe_OnAlreadySubscribedBody_Returns400()
    {
        var sector = await CreateSectorAsync(nameof(Subscribe_OnAlreadySubscribedBody_Returns400));
        var body = await CreateStandardBodyAsync(nameof(Subscribe_OnAlreadySubscribedBody_Returns400), sector.Id);

        var firstResponse = await AdminClient.PostAsync(SubscribeUrl(body.Id), null);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponse = await AdminClient.PostAsync(SubscribeUrl(body.Id), null);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── DELETE unsubscribe ───────────────────────────────────────────────────

    [Fact]
    public async Task Unsubscribe_RemovesSubscription()
    {
        var sector = await CreateSectorAsync(nameof(Unsubscribe_RemovesSubscription));
        var body = await CreateStandardBodyAsync(nameof(Unsubscribe_RemovesSubscription), sector.Id);

        var subscribeResponse = await AdminClient.PostAsync(SubscribeUrl(body.Id), null);
        subscribeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await AdminClient.DeleteAndGetResponseAsync(SubscribeUrl(body.Id));

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (response, subscribed) = await AdminClient.GetWithResponseAsync<List<TenantStandardSubscriptionDto>>(SubscribedUrl());
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        subscribed!.Select(s => s.RegulatoryBodyId).Should().NotContain(body.Id);
    }

    [Fact]
    public async Task Unsubscribe_NonExistentSubscription_Returns404()
    {
        var sector = await CreateSectorAsync(nameof(Unsubscribe_NonExistentSubscription_Returns404));
        var body = await CreateStandardBodyAsync(nameof(Unsubscribe_NonExistentSubscription_Returns404), sector.Id);

        var response = await AdminClient.DeleteAndGetResponseAsync(SubscribeUrl(body.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Unsubscribe_ForOtherTenantsSubscription_Returns404()
    {
        await EnsureTenantBExistsAsync();
        var tenantBClient = CreateTenantBAdminClient();

        var sector = await CreateSectorAsync(nameof(Unsubscribe_ForOtherTenantsSubscription_Returns404));
        var body = await CreateStandardBodyAsync(nameof(Unsubscribe_ForOtherTenantsSubscription_Returns404), sector.Id);

        // Tenant A subscribes; Tenant B never does.
        var subscribeResponse = await AdminClient.PostAsync(SubscribeUrl(body.Id), null);
        subscribeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Tenant B's admin, acting on their own (valid) tenantId, tries to unsubscribe a body
        // they never subscribed to. Tenant scoping means the row simply isn't found in their scope.
        var response = await tenantBClient.DeleteAndGetResponseAsync(
            SubscribeUrl(body.Id, TestTenantConstants.TenantB.TenantId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Tenant A's subscription is untouched.
        var context = GetDbContext();
        var reloaded = await context.TenantStandardSubscriptions
            .IgnoreQueryFilters()
            .SingleAsync(s => s.TenantId == TestTenantConstants.TenantId && s.RegulatoryBodyId == body.Id);
        reloaded.IsDeleted.Should().BeFalse();
    }

    // ── Authorization ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailable_WithoutLearningsAdminPermission_Returns403()
    {
        await AssertForbiddenAsync(OperatorClient, AvailableUrl());
    }

    [Fact]
    public async Task GetSubscribed_WithoutLearningsAdminPermission_Returns403()
    {
        await AssertForbiddenAsync(OperatorClient, SubscribedUrl());
    }

    [Fact]
    public async Task Subscribe_WithoutLearningsAdminPermission_Returns403()
    {
        var sector = await CreateSectorAsync(nameof(Subscribe_WithoutLearningsAdminPermission_Returns403));
        var body = await CreateStandardBodyAsync(nameof(Subscribe_WithoutLearningsAdminPermission_Returns403), sector.Id);

        var response = await OperatorClient.PostAsync(SubscribeUrl(body.Id), null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Unsubscribe_WithoutLearningsAdminPermission_Returns403()
    {
        var sector = await CreateSectorAsync(nameof(Unsubscribe_WithoutLearningsAdminPermission_Returns403));
        var body = await CreateStandardBodyAsync(nameof(Unsubscribe_WithoutLearningsAdminPermission_Returns403), sector.Id);

        var subscribeResponse = await AdminClient.PostAsync(SubscribeUrl(body.Id), null);
        subscribeResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await OperatorClient.DeleteAndGetResponseAsync(SubscribeUrl(body.Id));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
