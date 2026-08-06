using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Core.Infrastructure.Identity;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Tests.Common.TestTenant;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Integration tests for /api/tenant-reviewer-configurations (Low-Score External-Review Chunk 1).
///
/// Dispatch: AdminClient (Learnings.Admin) for mutation success paths.
///           OperatorClient (Learnings.View only) for read success + mutation 403 checks.
///           UnauthenticatedClient for 401 checks.
/// </summary>
[Collection("Integration")]
public class TenantReviewerConfigurationTests : IntegrationTestBase
{
    private const string BaseUrl = "/api/tenant-reviewer-configurations";

    public TenantReviewerConfigurationTests(CustomWebApplicationFactory factory)
        : base(factory) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static object ValidCreateBody(string? languageCode = "fr", string? email = null, string? name = null) => new
    {
        LanguageCode = languageCode,
        ReviewerEmail = email ?? $"reviewer.{Guid.NewGuid():N}@example.com",
        ReviewerName = name,
    };

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

    private async Task<TenantReviewerConfiguration?> GetEntityAsync(Guid id)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<TenantReviewerConfiguration>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);
    }

    private record ReviewerConfigDto(Guid Id, string? LanguageCode, string ReviewerEmail, string? ReviewerName);

    // ── tests ─────────────────────────────────────────────────────────────────

    // 1 — GetAll returns only rows scoped to the caller's tenant
    // Note: Respawner only resets the "public" schema, not "toolbox_talks" (where this
    // entity lives), so rows persist across test methods within a run — this test asserts
    // by Id rather than by exact list membership, matching the existing codebase convention
    // of using unique values instead of relying on a wipe (see UpdateToolboxTalkSettingsCommandHandlerTests.UniqueTitle).
    [Fact]
    public async Task GetAll_ReturnsTenantScopedRowsOnly()
    {
        await EnsureTenantBExistsAsync();
        var tenantBClient = CreateTenantBAdminClient();

        var createResponse = await AdminClient.PostAsJsonAsync(BaseUrl, ValidCreateBody("el"));
        createResponse.EnsureSuccessStatusCode();
        var tenantAConfig = await createResponse.Content.ReadFromJsonAsync<ReviewerConfigDto>();

        var tenantBCreateResponse = await tenantBClient.PostAsJsonAsync(BaseUrl, ValidCreateBody("hi"));
        tenantBCreateResponse.EnsureSuccessStatusCode();
        var tenantBConfig = await tenantBCreateResponse.Content.ReadFromJsonAsync<ReviewerConfigDto>();

        var tenantAList = await AdminClient.GetFromJsonAsync<List<ReviewerConfigDto>>(BaseUrl);
        tenantAList.Should().NotBeNull();
        tenantAList!.Should().Contain(c => c.Id == tenantAConfig!.Id);
        tenantAList.Should().NotContain(c => c.Id == tenantBConfig!.Id);
    }

    // 2 — Create with valid input succeeds
    [Fact]
    public async Task Create_ValidInput_Returns201WithDto()
    {
        var response = await AdminClient.PostAsJsonAsync(BaseUrl, ValidCreateBody("fr", "fr-reviewer@example.com", "French Reviewer"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await response.Content.ReadFromJsonAsync<ReviewerConfigDto>();
        dto.Should().NotBeNull();
        dto!.LanguageCode.Should().Be("fr");
        dto.ReviewerEmail.Should().Be("fr-reviewer@example.com");
        dto.ReviewerName.Should().Be("French Reviewer");
    }

    // 3 — Create with duplicate (tenant, language) fails with 409
    [Fact]
    public async Task Create_DuplicateLanguage_Returns409()
    {
        var first = await AdminClient.PostAsJsonAsync(BaseUrl, ValidCreateBody("de"));
        first.EnsureSuccessStatusCode();

        var second = await AdminClient.PostAsJsonAsync(BaseUrl, ValidCreateBody("de"));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 4 — Create with invalid email fails with 400
    [Fact]
    public async Task Create_InvalidEmail_Returns400()
    {
        var response = await AdminClient.PostAsJsonAsync(BaseUrl, ValidCreateBody("es", "not-an-email"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // 5 — Create with null language when a fallback row already exists fails with 409
    [Fact]
    public async Task Create_SecondFallbackRow_Returns409()
    {
        var first = await AdminClient.PostAsJsonAsync(BaseUrl, ValidCreateBody(null));
        first.EnsureSuccessStatusCode();

        var second = await AdminClient.PostAsJsonAsync(BaseUrl, ValidCreateBody(null));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // 6 — Update succeeds for a row owned by the caller's tenant
    [Fact]
    public async Task Update_OwnedRow_Returns200AndPersists()
    {
        var createResponse = await AdminClient.PostAsJsonAsync(BaseUrl, ValidCreateBody("ru"));
        var created = await createResponse.Content.ReadFromJsonAsync<ReviewerConfigDto>();

        var updateResponse = await AdminClient.PutAsJsonAsync($"{BaseUrl}/{created!.Id}", new
        {
            ReviewerEmail = "updated@example.com",
            ReviewerName = "Updated Name",
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var entity = await GetEntityAsync(created.Id);
        entity!.ReviewerEmail.Should().Be("updated@example.com");
        entity.ReviewerName.Should().Be("Updated Name");
    }

    // 7 — Update fails for a row not owned by the caller's tenant
    [Fact]
    public async Task Update_RowNotOwnedByTenant_Returns404()
    {
        await EnsureTenantBExistsAsync();
        var tenantBClient = CreateTenantBAdminClient();

        var createResponse = await AdminClient.PostAsJsonAsync(BaseUrl, ValidCreateBody("sv"));
        var created = await createResponse.Content.ReadFromJsonAsync<ReviewerConfigDto>();

        var updateResponse = await tenantBClient.PutAsJsonAsync($"{BaseUrl}/{created!.Id}", new
        {
            ReviewerEmail = "hijacked@example.com",
            ReviewerName = (string?)null,
        });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var entity = await GetEntityAsync(created.Id);
        entity!.ReviewerEmail.Should().NotBe("hijacked@example.com");
    }

    // 8 — Delete succeeds and hard-removes the row
    [Fact]
    public async Task Delete_OwnedRow_Returns204AndRemovesRow()
    {
        var createResponse = await AdminClient.PostAsJsonAsync(BaseUrl, ValidCreateBody("sk"));
        var created = await createResponse.Content.ReadFromJsonAsync<ReviewerConfigDto>();

        var deleteResponse = await AdminClient.DeleteAsync($"{BaseUrl}/{created!.Id}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var entity = await GetEntityAsync(created.Id);
        entity.Should().BeNull();
    }

    // 9 — Delete on a non-existent row fails cleanly with 404
    [Fact]
    public async Task Delete_NonExistentRow_Returns404()
    {
        var response = await AdminClient.DeleteAsync($"{BaseUrl}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // 10 — Unauthenticated → 401
    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        await AssertUnauthorizedAsync(BaseUrl);
    }

    // 11 — Operator (Learnings.View only, no Learnings.Admin) can read but not create
    [Fact]
    public async Task OperatorClient_CanRead_CannotCreate()
    {
        var getResponse = await OperatorClient.GetAsync(BaseUrl);
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createResponse = await OperatorClient.PostAsJsonAsync(BaseUrl, ValidCreateBody("pt"));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
