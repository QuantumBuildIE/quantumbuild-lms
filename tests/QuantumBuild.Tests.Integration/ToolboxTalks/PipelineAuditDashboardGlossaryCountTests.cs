using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// GET /api/toolbox-talks/pipeline/dashboard: glossary term counts are split into system and
/// tenant-override counts, both limited to the tenant's sectors.
///
/// The test DB already holds seeded system glossaries, so each test reads a baseline first and
/// asserts on the delta caused by its own rows, which use unique sector keys.
/// </summary>
[Collection("Integration")]
public class PipelineAuditDashboardGlossaryCountTests : IntegrationTestBase
{
    private const string DashboardUrl = "/api/toolbox-talks/pipeline/dashboard";

    private static readonly Guid TenantA = TestTenantConstants.TenantId;
    private static readonly Guid TenantB = TestTenantConstants.TenantB.TenantId;

    public PipelineAuditDashboardGlossaryCountTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private async Task<PipelineAuditDashboardDto> GetDashboardAsync(HttpClient client)
    {
        var response = await client.GetAsync(DashboardUrl);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<PipelineAuditDashboardDto>();
        dto.Should().NotBeNull();
        return dto!;
    }

    /// <summary>
    /// Tenant A has sector S. Rows added:
    ///   system S: 3 terms, system S2: 4 terms (A does not have S2),
    ///   A override S: 2 terms, A override S2: 6 terms, B override S: 5 terms.
    /// </summary>
    private async Task SeedAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var db = GetDbContext();

        var sectorS = new Sector { Key = $"itest-s-{suffix}", Name = "Integration S" };
        var sectorS2 = new Sector { Key = $"itest-s2-{suffix}", Name = "Integration S2" };
        db.Sectors.AddRange(sectorS, sectorS2);
        db.TenantSectors.Add(new TenantSector { TenantId = TenantA, Sector = sectorS });

        AddGlossary(db, null, sectorS.Key, 3);
        AddGlossary(db, null, sectorS2.Key, 4);
        AddGlossary(db, TenantA, sectorS.Key, 2);
        AddGlossary(db, TenantA, sectorS2.Key, 6);
        AddGlossary(db, TenantB, sectorS.Key, 5);

        await db.SaveChangesAsync();
    }

    private static void AddGlossary(
        QuantumBuild.Core.Infrastructure.Data.ApplicationDbContext db,
        Guid? tenantId, string sectorKey, int termCount)
    {
        var glossary = new SafetyGlossary
        {
            TenantId = tenantId,
            SectorKey = sectorKey,
            SectorName = sectorKey
        };
        for (var i = 0; i < termCount; i++)
        {
            glossary.Terms.Add(new SafetyGlossaryTerm
            {
                EnglishTerm = $"{sectorKey}-term-{i}",
                Category = "Test"
            });
        }
        db.SafetyGlossaries.Add(glossary);
    }

    [Fact]
    public async Task TenantUser_SeesOnlyTermsInOwnSectors()
    {
        var before = await GetDashboardAsync(AdminClient);

        await SeedAsync();

        var after = await GetDashboardAsync(AdminClient);
        (after.SystemGlossaryTermCount - before.SystemGlossaryTermCount).Should().Be(3);
        (after.TenantOverrideTermCount - before.TenantOverrideTermCount).Should().Be(2);
    }

    [Fact]
    public async Task SuperUserWithActiveTenant_SeesSameCountsAsTenantUser()
    {
        using var superUserClient = Factory.CreateSuperUserClient(TenantA);
        var before = await GetDashboardAsync(superUserClient);

        await SeedAsync();

        var after = await GetDashboardAsync(superUserClient);
        (after.SystemGlossaryTermCount - before.SystemGlossaryTermCount).Should().Be(3);
        (after.TenantOverrideTermCount - before.TenantOverrideTermCount).Should().Be(2);
    }
}
