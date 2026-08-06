using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Chunk 1 of the multi-standard regulatory feature (docs/regulatory-multi-standard-recon.md).
/// Covers the RegulatoryBody.Kind discriminator (Regulation vs Standard), the Kind/SectorId
/// consistency invariant, and the TenantStandardSubscription entity. No API surface exists yet
/// (added in later chunks) — everything here exercises the data model directly via DbContext.
/// </summary>
[Collection("Integration")]
public class RegulatoryBodyKindTests : IntegrationTestBase
{
    public RegulatoryBodyKindTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    private static Sector NewSector(string key, string uniqueSuffix) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Name = $"Kind Test Sector {uniqueSuffix}",
        DisplayOrder = 99,
        IsActive = true
    };

    private static RegulatoryBody NewBody(string uniqueSuffix, RegulatoryBodyKind? kind = null, Guid? sectorId = null)
    {
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = $"Kind Test Body {uniqueSuffix}",
            Code = $"KT{Guid.NewGuid():N}"[..15],
            Country = "IE",
            SectorId = sectorId
        };

        if (kind is not null)
            body.Kind = kind.Value;

        return body;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Kind backfill / default
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SeededRegulatoryBodies_AllReportKindRegulation()
    {
        var context = GetDbContext();

        var seededCodes = new[] { "HIQA", "HSA", "FSAI", "RSA" };
        var bodies = await context.RegulatoryBodies
            .Where(b => seededCodes.Contains(b.Code))
            .ToListAsync();

        bodies.Should().HaveCount(4);
        bodies.Should().OnlyContain(b => b.Kind == RegulatoryBodyKind.Regulation && b.SectorId == null);
    }

    [Fact]
    public async Task CreateBody_WithoutExplicitKind_DefaultsToRegulationWithNullSector()
    {
        var context = GetDbContext();
        var body = NewBody(nameof(CreateBody_WithoutExplicitKind_DefaultsToRegulationWithNullSector));

        context.RegulatoryBodies.Add(body);
        await context.SaveChangesAsync();

        var reloaded = await context.RegulatoryBodies.SingleAsync(b => b.Id == body.Id);
        reloaded.Kind.Should().Be(RegulatoryBodyKind.Regulation);
        reloaded.SectorId.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Kind / SectorId consistency invariant (DB check constraint)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateStandardBody_WithNullSectorId_ThrowsOnSave()
    {
        var context = GetDbContext();
        var body = NewBody(nameof(CreateStandardBody_WithNullSectorId_ThrowsOnSave), RegulatoryBodyKind.Standard, sectorId: null);
        context.RegulatoryBodies.Add(body);

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task CreateRegulationBody_WithNonNullSectorId_ThrowsOnSave()
    {
        var context = GetDbContext();
        var sector = NewSector("kind-test-reg-sector", nameof(CreateRegulationBody_WithNonNullSectorId_ThrowsOnSave));
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        var body = NewBody(nameof(CreateRegulationBody_WithNonNullSectorId_ThrowsOnSave), RegulatoryBodyKind.Regulation, sectorId: sector.Id);
        context.RegulatoryBodies.Add(body);

        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task CreateStandardBody_WithSectorId_Succeeds()
    {
        var context = GetDbContext();
        var sector = NewSector("kind-test-std-sector", nameof(CreateStandardBody_WithSectorId_Succeeds));
        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        var body = NewBody(nameof(CreateStandardBody_WithSectorId_Succeeds), RegulatoryBodyKind.Standard, sectorId: sector.Id);
        context.RegulatoryBodies.Add(body);
        await context.SaveChangesAsync();

        var reloaded = await context.RegulatoryBodies.SingleAsync(b => b.Id == body.Id);
        reloaded.Kind.Should().Be(RegulatoryBodyKind.Standard);
        reloaded.SectorId.Should().Be(sector.Id);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // TenantStandardSubscription
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSubscription_ToStandardBody_Succeeds()
    {
        var context = GetDbContext();
        var sector = NewSector("kind-test-sub-sector", nameof(CreateSubscription_ToStandardBody_Succeeds));
        var body = NewBody(nameof(CreateSubscription_ToStandardBody_Succeeds), RegulatoryBodyKind.Standard, sectorId: sector.Id);
        context.Sectors.Add(sector);
        context.RegulatoryBodies.Add(body);
        await context.SaveChangesAsync();

        var subscription = TenantStandardSubscription.Create(TestTenantConstants.TenantId, body);
        context.TenantStandardSubscriptions.Add(subscription);
        await context.SaveChangesAsync();

        var reloaded = await context.TenantStandardSubscriptions.IgnoreQueryFilters().SingleAsync(s => s.Id == subscription.Id);
        reloaded.TenantId.Should().Be(TestTenantConstants.TenantId);
        reloaded.RegulatoryBodyId.Should().Be(body.Id);
    }

    [Fact]
    public void CreateSubscription_ToRegulationBody_ThrowsInvalidOperationException()
    {
        var body = NewBody(nameof(CreateSubscription_ToRegulationBody_ThrowsInvalidOperationException), RegulatoryBodyKind.Regulation);

        var act = () => TenantStandardSubscription.Create(TestTenantConstants.TenantId, body);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateDuplicateSubscription_ForSameTenantAndBody_ThrowsOnSave()
    {
        var context = GetDbContext();
        var sector = NewSector("kind-test-dup-sector", nameof(CreateDuplicateSubscription_ForSameTenantAndBody_ThrowsOnSave));
        var body = NewBody(nameof(CreateDuplicateSubscription_ForSameTenantAndBody_ThrowsOnSave), RegulatoryBodyKind.Standard, sectorId: sector.Id);
        context.Sectors.Add(sector);
        context.RegulatoryBodies.Add(body);
        await context.SaveChangesAsync();

        context.TenantStandardSubscriptions.Add(TenantStandardSubscription.Create(TestTenantConstants.TenantId, body));
        await context.SaveChangesAsync();

        context.TenantStandardSubscriptions.Add(TenantStandardSubscription.Create(TestTenantConstants.TenantId, body));
        var act = async () => await context.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// Design decision: sector tagging on a Standard body is a soft constraint (admin
    /// interpretation, no automatic merge/precedence — see docs/regulatory-multi-standard-recon.md).
    /// A tenant may subscribe to a standard whose SectorId is not among the tenant's own
    /// TenantSector rows. Nothing in this chunk enforces membership — confirmed here so a future
    /// chunk doesn't assume enforcement exists at the data layer.
    /// </summary>
    [Fact]
    public async Task CreateSubscription_ForSectorNotInTenantsOwnSectors_IsAllowed()
    {
        var context = GetDbContext();

        // Tenant's own sector (unrelated to the standard body's tagged sector)
        var tenantSector = NewSector("kind-test-tenant-sector", nameof(CreateSubscription_ForSectorNotInTenantsOwnSectors_IsAllowed) + "-tenant");
        context.Sectors.Add(tenantSector);
        await context.SaveChangesAsync();
        context.TenantSectors.Add(new TenantSector
        {
            Id = Guid.NewGuid(),
            TenantId = TestTenantConstants.TenantId,
            SectorId = tenantSector.Id,
            IsDefault = true
        });
        await context.SaveChangesAsync();

        // Standard body tagged to a different sector entirely
        var standardSector = NewSector("kind-test-standard-sector", nameof(CreateSubscription_ForSectorNotInTenantsOwnSectors_IsAllowed) + "-standard");
        var body = NewBody(nameof(CreateSubscription_ForSectorNotInTenantsOwnSectors_IsAllowed), RegulatoryBodyKind.Standard, sectorId: standardSector.Id);
        context.Sectors.Add(standardSector);
        context.RegulatoryBodies.Add(body);
        await context.SaveChangesAsync();

        var subscription = TenantStandardSubscription.Create(TestTenantConstants.TenantId, body);
        context.TenantStandardSubscriptions.Add(subscription);
        await context.SaveChangesAsync();

        var reloaded = await context.TenantStandardSubscriptions.IgnoreQueryFilters().SingleOrDefaultAsync(s => s.Id == subscription.Id);
        reloaded.Should().NotBeNull();
    }
}
