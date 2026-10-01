using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Identity;

namespace QuantumBuild.Tests.Integration.Core;

/// <summary>
/// Tenant logo backend: PUT/DELETE /api/tenants/{tenantId}/branding/logo (Tenant.Manage, route tenant only)
/// and GET /api/branding/current (any authenticated user, current tenant). TenantBranding has no tenant
/// query filter, so the SuperUser-with-another-active-tenant cases are the load-bearing ones.
/// </summary>
[Collection("Integration")]
public class TenantBrandingTests : IntegrationTestBase
{
    // Minimal valid 1x1 PNG and a JPEG header + padding
    private static readonly byte[] PngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    public TenantBrandingTests(CustomWebApplicationFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        FakeR2StorageService.Reset();
        Factory.BrandingConflictInterceptor.Disarm();
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task<Guid> CreateTenantAsync()
    {
        var context = GetDbContext();
        var id = Guid.NewGuid();
        context.Tenants.Add(new Tenant { Id = id, Name = $"Branding {id:N}"[..20], Code = $"B{id:N}"[..10] });
        await context.SaveChangesAsync();
        return id;
    }

    private static MultipartFormDataContent FileContent(byte[] bytes, string contentType = "image/png", string fileName = "logo.png")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid tenantId, byte[] bytes,
        string contentType = "image/png", string fileName = "logo.png") =>
        client.PutAsync($"/api/tenants/{tenantId}/branding/logo", FileContent(bytes, contentType, fileName));

    private HttpClient TenantAdminClient(Guid tenantId) =>
        Factory.CreateAuthenticatedClient(
            Guid.NewGuid(), "tenantadmin@test.quantumbuild.ie", tenantId,
            new[] { "Admin" },
            Permissions.GetAll().Where(p => p != Permissions.Tenant.Manage).ToArray());

    private async Task<List<TenantBranding>> BrandingRowsAsync(Guid tenantId) =>
        await GetDbContext().TenantBrandings.Where(b => b.TenantId == tenantId).ToListAsync();

    private IEnumerable<string> KeysUnder(Guid tenantId) =>
        FakeR2StorageService.StoredFiles.Keys.Where(k => k.StartsWith($"{tenantId}/"));

    private static async Task<string?> LogoUrlAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<LogoDto>();
        return body?.LogoUrl;
    }

    private record LogoDto(string? LogoUrl);

    // ── upload ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Upload_Png_Returns200_AndCreatesRowAndObject()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();

        var response = await UploadAsync(client, tenant, PngBytes);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await BrandingRowsAsync(tenant);
        rows.Should().ContainSingle();
        rows[0].LogoKey.Should().StartWith($"{tenant}/branding/logo-").And.EndWith(".png");
        FakeR2StorageService.StoredFiles.Should().ContainKey(rows[0].LogoKey!);
        (await LogoUrlAsync(response)).Should().EndWith(rows[0].LogoKey!);
    }

    [Fact]
    public async Task Upload_Jpeg_Returns200_WithJpgKey()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();

        var response = await UploadAsync(client, tenant, JpegBytes, "image/jpeg", "logo.jpeg");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BrandingRowsAsync(tenant)).Single().LogoKey.Should().EndWith(".jpg");
    }

    [Fact]
    public async Task Upload_Twice_UsesNewKey_DeletesOldObject_KeepsSingleRow()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();

        (await UploadAsync(client, tenant, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        var firstKey = (await BrandingRowsAsync(tenant)).Single().LogoKey!;

        (await UploadAsync(client, tenant, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);

        var rows = await BrandingRowsAsync(tenant);
        rows.Should().ContainSingle();
        rows[0].LogoKey.Should().NotBe(firstKey);
        FakeR2StorageService.StoredFiles.Should().NotContainKey(firstKey);
        FakeR2StorageService.StoredFiles.Should().ContainKey(rows[0].LogoKey!);
    }

    [Fact]
    public async Task Upload_TextLabelledAsPng_Returns400()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();

        var response = await UploadAsync(client, tenant, Encoding.UTF8.GetBytes("this is not an image"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BrandingRowsAsync(tenant)).Should().BeEmpty();
        KeysUnder(tenant).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_RealSvg_Returns400()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        var response = await UploadAsync(client, tenant, svg, "image/svg+xml", "logo.svg");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        KeysUnder(tenant).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_OverOneMegabyte_Returns400()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();
        var big = new byte[1024 * 1024 + 1];
        PngBytes.CopyTo(big, 0);

        var response = await UploadAsync(client, tenant, big);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        KeysUnder(tenant).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_GuidEmptyTenant_Returns400()
    {
        using var client = Factory.CreateSuperUserClient();

        var response = await UploadAsync(client, Guid.Empty, PngBytes);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        KeysUnder(Guid.Empty).Should().BeEmpty();
    }

    [Fact]
    public async Task Upload_UnknownTenant_Returns404()
    {
        var unknown = Guid.NewGuid();
        using var client = Factory.CreateSuperUserClient();

        var response = await UploadAsync(client, unknown, PngBytes);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        KeysUnder(unknown).Should().BeEmpty();
    }

    // ── authorisation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task TenantAdmin_UploadForOwnTenant_Returns403()
    {
        var tenant = await CreateTenantAsync();
        using var client = TenantAdminClient(tenant);

        var response = await UploadAsync(client, tenant, PngBytes);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await BrandingRowsAsync(tenant)).Should().BeEmpty();
        KeysUnder(tenant).Should().BeEmpty();
    }

    [Fact]
    public async Task TenantAdmin_DeleteOwnTenantLogo_Returns403()
    {
        var tenant = await CreateTenantAsync();
        using (var superUser = Factory.CreateSuperUserClient())
            (await UploadAsync(superUser, tenant, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var client = TenantAdminClient(tenant);

        var response = await client.DeleteAsync($"/api/tenants/{tenant}/branding/logo");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await BrandingRowsAsync(tenant)).Single().LogoKey.Should().NotBeNull();
    }

    // ── SuperUser tenant targeting ─────────────────────────────────────────────

    [Fact]
    public async Task SuperUser_ActiveTenantX_UploadsLogoForTenantY_WritesY_LeavesXUntouched()
    {
        var x = await CreateTenantAsync();
        var y = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient(activeTenantId: x);

        (await UploadAsync(client, y, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);

        var yRows = await BrandingRowsAsync(y);
        yRows.Should().ContainSingle();
        yRows[0].LogoKey.Should().StartWith($"{y}/branding/");
        (await BrandingRowsAsync(x)).Should().BeEmpty();
        KeysUnder(x).Should().BeEmpty();

        // Second upload must find Y's existing row (no tenant filter) rather than collide on the unique index
        (await UploadAsync(client, y, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await BrandingRowsAsync(y)).Should().ContainSingle();
        (await BrandingRowsAsync(x)).Should().BeEmpty();
        KeysUnder(x).Should().BeEmpty();
        KeysUnder(y).Should().ContainSingle();
    }

    [Fact]
    public async Task SuperUser_NoActiveTenant_UploadsLogoForTenantY_Succeeds()
    {
        var y = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();

        (await UploadAsync(client, y, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await BrandingRowsAsync(y)).Should().ContainSingle();
        (await GetDbContext().TenantBrandings.Where(b => b.TenantId == Guid.Empty).ToListAsync()).Should().BeEmpty();
        KeysUnder(Guid.Empty).Should().BeEmpty();
    }

    // ── concurrent first upload ────────────────────────────────────────────────

    [Fact]
    public async Task Upload_ConcurrentFirstUpload_RetriesAsUpdate_LastWriteWins_NoOrphans()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();
        var rivalKey = $"{tenant}/branding/logo-rival.png";
        Factory.BrandingConflictInterceptor.Arm(async () =>
        {
            await FakeR2StorageService.UploadTenantLogoAsync(tenant, "logo-rival.png", PngBytes, "image/png");
            var other = GetDbContext();
            other.TenantBrandings.Add(new TenantBranding { Id = Guid.NewGuid(), TenantId = tenant, LogoKey = rivalKey });
            await other.SaveChangesAsync();
        });

        var response = await UploadAsync(client, tenant, PngBytes);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await BrandingRowsAsync(tenant);
        rows.Should().ContainSingle();
        rows[0].LogoKey.Should().NotBe(rivalKey).And.StartWith($"{tenant}/branding/logo-");
        FakeR2StorageService.StoredFiles.Should().NotContainKey(rivalKey);
        KeysUnder(tenant).Should().BeEquivalentTo(new[] { rows[0].LogoKey! });
        (await LogoUrlAsync(response)).Should().EndWith(rows[0].LogoKey!);
    }

    // ── delete ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_NullsLogoKey_RetainsRow_RemovesObject_AndReuploadSucceeds()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();
        (await UploadAsync(client, tenant, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        var key = (await BrandingRowsAsync(tenant)).Single().LogoKey!;

        var response = await client.DeleteAsync($"/api/tenants/{tenant}/branding/logo");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rows = await BrandingRowsAsync(tenant);
        rows.Should().ContainSingle();
        rows[0].LogoKey.Should().BeNull();
        FakeR2StorageService.StoredFiles.Should().NotContainKey(key);

        (await UploadAsync(client, tenant, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await BrandingRowsAsync(tenant);
        after.Should().ContainSingle();
        after[0].LogoKey.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_WhenNoLogoSet_Returns204()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();

        var response = await client.DeleteAsync($"/api/tenants/{tenant}/branding/logo");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── GET /api/branding/current ──────────────────────────────────────────────

    [Fact]
    public async Task GetCurrent_TenantUser_ReturnsOwnLogo_EvenWithAnotherTenantsHeader()
    {
        var own = await CreateTenantAsync();
        var other = await CreateTenantAsync();
        using (var superUser = Factory.CreateSuperUserClient())
        {
            (await UploadAsync(superUser, own, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await UploadAsync(superUser, other, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
        var ownKey = (await BrandingRowsAsync(own)).Single().LogoKey!;

        using var client = TenantAdminClient(own);
        var plain = await client.GetAsync("/api/branding/current");
        plain.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LogoUrlAsync(plain)).Should().EndWith(ownKey);

        client.DefaultRequestHeaders.Add("X-Tenant-Id", other.ToString());
        var withForeignHeader = await client.GetAsync("/api/branding/current");
        (await LogoUrlAsync(withForeignHeader)).Should().EndWith(ownKey);
    }

    [Fact]
    public async Task GetCurrent_SuperUserWithActiveTenant_ReturnsThatTenantsLogo()
    {
        var x = await CreateTenantAsync();
        using (var setup = Factory.CreateSuperUserClient())
            (await UploadAsync(setup, x, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        var key = (await BrandingRowsAsync(x)).Single().LogoKey!;

        using var client = Factory.CreateSuperUserClient(activeTenantId: x);
        var response = await client.GetAsync("/api/branding/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LogoUrlAsync(response)).Should().EndWith(key);
    }

    [Fact]
    public async Task GetCurrent_SuperUserWithNoActiveTenant_ReturnsNull()
    {
        using var client = Factory.CreateSuperUserClient();

        var response = await client.GetAsync("/api/branding/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LogoUrlAsync(response)).Should().BeNull();
    }

    [Fact]
    public async Task GetCurrent_TenantWithNoRow_ReturnsNull()
    {
        var tenant = await CreateTenantAsync();
        using var client = TenantAdminClient(tenant);

        var response = await client.GetAsync("/api/branding/current");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await LogoUrlAsync(response)).Should().BeNull();
    }

    [Fact]
    public async Task GetCurrent_AfterDelete_ReturnsNull()
    {
        var tenant = await CreateTenantAsync();
        using var superUser = Factory.CreateSuperUserClient();
        (await UploadAsync(superUser, tenant, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await superUser.DeleteAsync($"/api/tenants/{tenant}/branding/logo")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var client = TenantAdminClient(tenant);
        var response = await client.GetAsync("/api/branding/current");

        (await LogoUrlAsync(response)).Should().BeNull();
    }

    [Fact]
    public async Task GetCurrent_Unauthenticated_Returns401()
    {
        await AssertUnauthorizedAsync("/api/branding/current");
    }

    // ── reset data / tenant update ─────────────────────────────────────────────

    [Fact]
    public async Task ResetData_KeepsLogoObjectAndRow_DeletesOtherFiles()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();
        (await UploadAsync(client, tenant, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        var logoKey = (await BrandingRowsAsync(tenant)).Single().LogoKey!;
        await FakeR2StorageService.UploadVideoAsync(tenant, Guid.NewGuid(), "t", new MemoryStream([1, 2, 3]), "v.mp4");
        KeysUnder(tenant).Should().HaveCount(2);

        var response = await client.PostAsync($"/api/tenants/{tenant}/reset-data", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        KeysUnder(tenant).Should().BeEquivalentTo(new[] { logoKey });
        (await BrandingRowsAsync(tenant)).Single().LogoKey.Should().Be(logoKey);
    }

    [Fact]
    public async Task UpdateTenant_AfterLogoSet_LeavesLogoUnchanged()
    {
        var tenant = await CreateTenantAsync();
        using var client = Factory.CreateSuperUserClient();
        (await UploadAsync(client, tenant, PngBytes)).StatusCode.Should().Be(HttpStatusCode.OK);
        var logoKey = (await BrandingRowsAsync(tenant)).Single().LogoKey!;

        var response = await client.PutAsJsonAsync($"/api/tenants/{tenant}",
            new { name = "Renamed Tenant", code = (string?)null, companyName = "Renamed Co", contactEmail = (string?)null, contactName = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await BrandingRowsAsync(tenant)).Single().LogoKey.Should().Be(logoKey);
        FakeR2StorageService.StoredFiles.Should().ContainKey(logoKey);
    }
}
