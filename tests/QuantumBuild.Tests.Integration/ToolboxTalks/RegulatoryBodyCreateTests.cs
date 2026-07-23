using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Chunk 2 of the multi-standard regulatory feature: the admin catalog surface for creating
/// RegulatoryBody rows (Regulations and Standards) via POST /api/regulatory/bodies, and the
/// Kind/SectorId-aware GET /api/regulatory/bodies used by the catalog list and pickers.
/// RegulatoryBody is a system-managed entity with no TenantId, so there is no tenant-scoping
/// dimension to test here.
/// </summary>
[Collection("Integration")]
public class RegulatoryBodyCreateTests : IntegrationTestBase
{
    public RegulatoryBodyCreateTests(CustomWebApplicationFactory factory) : base(factory) { }

    private async Task<Sector> CreateSectorAsync(string uniqueSuffix)
    {
        var context = GetDbContext();
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Key = $"bct-{Guid.NewGuid():N}"[..20],
            Name = $"Body Create Test Sector {uniqueSuffix}",
            DisplayOrder = 99,
            IsActive = true
        };
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();
        return sector;
    }

    private static string UniqueCode(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..15];

    // ─────────────────────────────────────────────────────────────────────────────
    // POST /api/regulatory/bodies — Kind/SectorId invariant
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBody_Regulation_WithoutSectorId_Succeeds()
    {
        var request = new CreateRegulatoryBodyRequest
        {
            Name = "Test Regulation Body",
            Code = UniqueCode("REG"),
            Country = "IE",
            Kind = RegulatoryBodyKind.Regulation,
            SectorId = null
        };

        var (response, dto) = await AdminClient.PostWithResponseAsync<CreateRegulatoryBodyRequest, RegulatoryBodyDto>(
            "/api/regulatory/bodies", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.Kind.Should().Be(nameof(RegulatoryBodyKind.Regulation));
        dto.SectorId.Should().BeNull();
        dto.SectorName.Should().BeNull();

        var context = GetDbContext();
        var reloaded = await context.RegulatoryBodies.SingleAsync(b => b.Id == dto.Id);
        reloaded.Kind.Should().Be(RegulatoryBodyKind.Regulation);
        reloaded.SectorId.Should().BeNull();
    }

    [Fact]
    public async Task CreateBody_Regulation_WithSectorId_Returns400()
    {
        var sector = await CreateSectorAsync(nameof(CreateBody_Regulation_WithSectorId_Returns400));

        var request = new CreateRegulatoryBodyRequest
        {
            Name = "Invalid Regulation Body",
            Code = UniqueCode("BAD"),
            Country = "IE",
            Kind = RegulatoryBodyKind.Regulation,
            SectorId = sector.Id
        };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryBodyRequest, RegulatoryBodyDto>(
            "/api/regulatory/bodies", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBody_Standard_WithoutSectorId_Returns400()
    {
        var request = new CreateRegulatoryBodyRequest
        {
            Name = "Invalid Standard Body",
            Code = UniqueCode("BAD"),
            Country = "IE",
            Kind = RegulatoryBodyKind.Standard,
            SectorId = null
        };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryBodyRequest, RegulatoryBodyDto>(
            "/api/regulatory/bodies", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBody_Standard_WithValidSectorId_Succeeds()
    {
        var sector = await CreateSectorAsync(nameof(CreateBody_Standard_WithValidSectorId_Succeeds));

        var request = new CreateRegulatoryBodyRequest
        {
            Name = "Test Standard Body",
            Code = UniqueCode("STD"),
            Country = "International",
            Kind = RegulatoryBodyKind.Standard,
            SectorId = sector.Id
        };

        var (response, dto) = await AdminClient.PostWithResponseAsync<CreateRegulatoryBodyRequest, RegulatoryBodyDto>(
            "/api/regulatory/bodies", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.Kind.Should().Be(nameof(RegulatoryBodyKind.Standard));
        dto.SectorId.Should().Be(sector.Id);
        dto.SectorName.Should().Be(sector.Name);

        var context = GetDbContext();
        var reloaded = await context.RegulatoryBodies.SingleAsync(b => b.Id == dto.Id);
        reloaded.Kind.Should().Be(RegulatoryBodyKind.Standard);
        reloaded.SectorId.Should().Be(sector.Id);
    }

    [Fact]
    public async Task CreateBody_Standard_WithNonExistentSectorId_Returns400()
    {
        var request = new CreateRegulatoryBodyRequest
        {
            Name = "Orphan Standard Body",
            Code = UniqueCode("ORP"),
            Country = "International",
            Kind = RegulatoryBodyKind.Standard,
            SectorId = Guid.NewGuid()
        };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryBodyRequest, RegulatoryBodyDto>(
            "/api/regulatory/bodies", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBody_DefaultsKindToRegulation_WhenNotSpecifiedByCaller()
    {
        // Mirrors CreateBody_WithoutExplicitKind_DefaultsToRegulationWithNullSector in
        // RegulatoryBodyKindTests, but through the API surface: a client that omits Kind
        // entirely (JSON deserializes to the record default) must not need SectorId.
        var request = new
        {
            Name = "Legacy-Shaped Body",
            Code = UniqueCode("LEG"),
            Country = "IE"
        };

        var (response, dto) = await AdminClient.PostWithResponseAsync<object, RegulatoryBodyDto>(
            "/api/regulatory/bodies", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto!.Kind.Should().Be(nameof(RegulatoryBodyKind.Regulation));
        dto.SectorId.Should().BeNull();
    }

    [Fact]
    public async Task CreateBody_DuplicateCode_Returns400()
    {
        var code = UniqueCode("DUP");
        var request = new CreateRegulatoryBodyRequest
        {
            Name = "First Body",
            Code = code,
            Country = "IE",
            Kind = RegulatoryBodyKind.Regulation
        };

        var (firstResponse, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryBodyRequest, RegulatoryBodyDto>(
            "/api/regulatory/bodies", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var duplicateRequest = request with { Name = "Second Body" };
        var (secondResponse, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryBodyRequest, RegulatoryBodyDto>(
            "/api/regulatory/bodies", duplicateRequest);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBody_EmptyName_Returns400()
    {
        var request = new CreateRegulatoryBodyRequest
        {
            Name = "",
            Code = UniqueCode("EMP"),
            Country = "IE"
        };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryBodyRequest, RegulatoryBodyDto>(
            "/api/regulatory/bodies", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateBody_WithoutTenantManagePermission_Returns403()
    {
        var request = new CreateRegulatoryBodyRequest
        {
            Name = "Forbidden Body",
            Code = UniqueCode("FBD"),
            Country = "IE"
        };

        var response = await OperatorClient.PostAsJsonAsync("/api/regulatory/bodies", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // GET /api/regulatory/bodies — Kind/Sector exposure + filter
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBodies_ReturnsKindAndSectorForStandardEntries()
    {
        var sector = await CreateSectorAsync(nameof(GetBodies_ReturnsKindAndSectorForStandardEntries));
        var context = GetDbContext();
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = "Listed Standard Body",
            Code = UniqueCode("LST"),
            Country = "International",
            Kind = RegulatoryBodyKind.Standard,
            SectorId = sector.Id
        };
        context.RegulatoryBodies.Add(body);
        await context.SaveChangesAsync();

        var (response, bodies) = await AdminClient.GetWithResponseAsync<List<RegulatoryBodyDto>>(
            "/api/regulatory/bodies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var listed = bodies!.Single(b => b.Id == body.Id);
        listed.Kind.Should().Be(nameof(RegulatoryBodyKind.Standard));
        listed.SectorId.Should().Be(sector.Id);
        listed.SectorName.Should().Be(sector.Name);
    }

    [Fact]
    public async Task GetBodies_FilteredByKindStandard_ExcludesRegulationBodies()
    {
        var sector = await CreateSectorAsync(nameof(GetBodies_FilteredByKindStandard_ExcludesRegulationBodies));
        var context = GetDbContext();

        var regulationBody = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = "Filter Test Regulation",
            Code = UniqueCode("FRG"),
            Country = "IE",
            Kind = RegulatoryBodyKind.Regulation
        };
        var standardBody = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = "Filter Test Standard",
            Code = UniqueCode("FST"),
            Country = "International",
            Kind = RegulatoryBodyKind.Standard,
            SectorId = sector.Id
        };
        context.RegulatoryBodies.AddRange(regulationBody, standardBody);
        await context.SaveChangesAsync();

        var (response, bodies) = await AdminClient.GetWithResponseAsync<List<RegulatoryBodyDto>>(
            "/api/regulatory/bodies?kind=Standard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        bodies!.Select(b => b.Id).Should().Contain(standardBody.Id);
        bodies!.Select(b => b.Id).Should().NotContain(regulationBody.Id);
        bodies!.Should().OnlyContain(b => b.Kind == nameof(RegulatoryBodyKind.Standard));
    }

    [Fact]
    public async Task GetBodies_WithoutTenantManagePermission_Returns403()
    {
        await AssertForbiddenAsync(OperatorClient, "/api/regulatory/bodies");
    }
}
