using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using QuantumBuild.Core.Domain.Entities;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Covers the two bugs fixed in certificate number allocation (see
/// docs/certificate-number-race-fix.md): a deterministic cross-tenant collision on the old
/// global unique index, and an intermittent within-tenant race from COUNT(*)-then-increment.
/// Exercises CertificateGenerationService directly against the real Postgres test container
/// (migrations already applied by CustomWebApplicationFactory), so both the new atomic
/// allocation logic and the tenant-scoped unique index migration are proven together.
///
/// Uses freshly generated tenant IDs per test rather than TestTenantConstants.TenantId:
/// Respawner's reset (CustomWebApplicationFactory) is scoped to SchemasToInclude = ["public"],
/// which does not cover the "toolbox_talks" schema these tables live in, so certificate/counter
/// rows for the shared test tenant accumulate across the whole test run. A fresh tenant ID is
/// guaranteed to have no prior certificates, which is what makes "first number is 000001"
/// assertions deterministic here.
/// </summary>
[Collection("Integration")]
public class CertificateNumberAllocationTests : IntegrationTestBase
{
    public CertificateNumberAllocationTests(CustomWebApplicationFactory factory) : base(factory) { }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        // FakeR2StorageService is a singleton shared across the whole "Integration" test
        // collection (see CustomWebApplicationFactory) - reset here so a prior test class that
        // set ShouldThrowOnUploadCertificate=true (e.g. CertificateEmailTests) can't bleed into
        // these tests, matching the same defensive reset CertificateEmailTests does for itself.
        FakeR2StorageService.Reset();
    }

    private static async Task<Guid> SeedEmployeeAsync(ApplicationDbContext db, Guid tenantId)
    {
        var id = Guid.NewGuid();
        db.Set<Employee>().Add(new Employee
        {
            Id = id,
            TenantId = tenantId,
            EmployeeCode = $"E{id:N}"[..10],
            FirstName = "Cert",
            LastName = "Test",
            IsActive = true
        });
        await db.SaveChangesAsync();
        return id;
    }

    private static async Task<ScheduledTalk> SeedScheduledTalkAsync(ApplicationDbContext db, Guid tenantId, Guid employeeId)
    {
        var talkId = Guid.NewGuid();
        db.Set<ToolboxTalk>().Add(new ToolboxTalk
        {
            Id = talkId,
            TenantId = tenantId,
            Code = $"T{Guid.NewGuid():N}"[..10],
            Title = "Cert Number Test Talk",
            GenerateCertificate = true
        });

        var scheduledTalk = new ScheduledTalk
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ToolboxTalkId = talkId,
            EmployeeId = employeeId,
            RequiredDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(7),
            Status = ScheduledTalkStatus.Completed,
            LanguageCode = "en"
        };
        db.Set<ScheduledTalk>().Add(scheduledTalk);

        await db.SaveChangesAsync();
        return scheduledTalk;
    }

    /// <summary>
    /// Two different tenants both on the default "LRN" prefix issue their first certificate of
    /// the year concurrently. Before the fix this deterministically collided on the old global
    /// unique index (both compute LRN-{year}-000001). After the fix, tenant-scoped numbering and
    /// a tenant-scoped unique index mean both succeed with the same-looking but distinct number.
    /// </summary>
    [Fact]
    public async Task GenerateTalkCertificate_ForTwoDifferentTenants_BothGetFirstNumber_NoCollision()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using var seedScope = Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var employeeA = await SeedEmployeeAsync(seedDb, tenantA);
        var employeeB = await SeedEmployeeAsync(seedDb, tenantB);
        var scheduledTalkA = await SeedScheduledTalkAsync(seedDb, tenantA, employeeA);
        var scheduledTalkB = await SeedScheduledTalkAsync(seedDb, tenantB, employeeB);

        // Act - fire both "first certificate of the year" generations concurrently, as would
        // happen if two tenants' employees completed training in the same moment. Each uses its
        // own DI scope (and therefore its own DbContext) - a DbContext instance is not
        // thread-safe for concurrent operations, and two real concurrent HTTP requests would
        // never share one either.
        async Task<ToolboxTalkCertificate?> GenerateAsync(ScheduledTalk scheduledTalk)
        {
            using var callScope = Factory.Services.CreateScope();
            var service = callScope.ServiceProvider.GetRequiredService<ICertificateGenerationService>();
            return await service.GenerateTalkCertificateAsync(scheduledTalk, null);
        }

        var results = await Task.WhenAll(GenerateAsync(scheduledTalkA), GenerateAsync(scheduledTalkB));

        var certA = results[0];
        var certB = results[1];

        certA.Should().NotBeNull("tenant A's certificate must be created without a unique-index violation");
        certB.Should().NotBeNull("tenant B's certificate must be created without a unique-index violation");

        var year = DateTime.UtcNow.Year;
        certA!.CertificateNumber.Should().Be($"LRN-{year}-000001");
        certB!.CertificateNumber.Should().Be($"LRN-{year}-000001",
            "each tenant numbers independently starting from 1, and the unique index is now tenant-scoped so this must not collide");
        certA.TenantId.Should().Be(tenantA);
        certB.TenantId.Should().Be(tenantB);
    }

    /// <summary>
    /// Many completions for the SAME tenant/prefix/year fire concurrently. Before the fix, the
    /// COUNT(*)-then-increment read could go stale during PDF render + R2 upload, causing two
    /// callers to compute the same number and one insert to fail on the unique index. After the
    /// fix, allocation is a single atomic upsert, so all callers must get distinct numbers.
    /// </summary>
    [Fact]
    public async Task GenerateTalkCertificate_ManyConcurrentCompletions_SameTenant_AllGetDistinctNumbers()
    {
        const int concurrentCount = 15;
        var tenantId = Guid.NewGuid();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var employeeId = await SeedEmployeeAsync(db, tenantId);

        var scheduledTalks = new List<ScheduledTalk>();
        for (var i = 0; i < concurrentCount; i++)
        {
            scheduledTalks.Add(await SeedScheduledTalkAsync(db, tenantId, employeeId));
        }

        // Act - each concurrent call uses its own DI scope (and therefore its own DbContext),
        // matching how concurrent HTTP requests would each get a scoped CertificateGenerationService.
        var tasks = scheduledTalks.Select(async st =>
        {
            using var callScope = Factory.Services.CreateScope();
            var service = callScope.ServiceProvider.GetRequiredService<ICertificateGenerationService>();
            return await service.GenerateTalkCertificateAsync(st, null);
        });
        var results = await Task.WhenAll(tasks);

        // Assert - every call succeeded and every certificate number is distinct.
        results.Should().OnlyContain(c => c != null, "no concurrent allocation should fail or collide");
        var numbers = results.Select(c => c!.CertificateNumber).ToList();
        numbers.Should().OnlyHaveUniqueItems("concurrent allocations for the same tenant/prefix/year must never produce duplicate numbers");
        numbers.Should().HaveCount(concurrentCount);

        // Allocation order across concurrent calls is inherently non-deterministic, so compare
        // the resulting set of numbers rather than positional order.
        var year = DateTime.UtcNow.Year;
        var expected = Enumerable.Range(1, concurrentCount).Select(n => $"LRN-{year}-{n:D6}");
        numbers.OrderBy(n => n, StringComparer.Ordinal).Should().Equal(
            expected.OrderBy(n => n, StringComparer.Ordinal),
            "the full 1..N run must be allocated with no duplicates and no unexpected values");
    }

    /// <summary>
    /// Reproduces the bug fixed by the seeding SQL added to migration
    /// 20260807093155_AddCertificateNumberCountersAndFixCertificateNumberIndexScope: without
    /// seeding, an "established" tenant - one with certificates already on disk from before
    /// CertificateNumberCounters existed - has no counter row, so its first post-migration
    /// allocation reads LastNumber as absent, inserts a fresh row starting at 1, and collides
    /// with the tenant's own pre-existing LRN-{year}-000001 certificate via the unique index.
    /// EF migrations cannot be re-invoked individually against an already-migrated test database,
    /// so this runs the seeding statement verbatim (copied from that migration's Up()) rather than
    /// re-running the whole migration, then proves the next live allocation continues at N+1.
    /// </summary>
    [Fact]
    public async Task GenerateTalkCertificate_ForEstablishedTenantAfterMigrationSeeding_ContinuesAtNPlusOne()
    {
        var tenantId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;

        using var seedScope = Factory.Services.CreateScope();
        var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var employeeId = await SeedEmployeeAsync(seedDb, tenantId);

        // Five certificates inserted directly (not via GenerateCertificateNumber), so no
        // CertificateNumberCounters row exists yet for this tenant - matching exactly the
        // "established tenant, empty counters table" state the migration seeding must fix.
        for (var i = 1; i <= 5; i++)
        {
            seedDb.Set<ToolboxTalkCertificate>().Add(new ToolboxTalkCertificate
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                EmployeeId = employeeId,
                CertificateType = CertificateType.Talk,
                CertificateNumber = $"LRN-{year}-{i:D6}",
                IssuedAt = DateTime.UtcNow,
                PdfStoragePath = $"fake/pre-existing-{i}.pdf",
                EmployeeName = "Cert Test",
                TrainingTitle = "Pre-existing Talk",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test-seed"
            });
        }
        await seedDb.SaveChangesAsync();

        // Act - the migration's seeding SQL verbatim, restricted to this test's own tenant (the
        // production migration scans the whole table, which is safe there because it runs exactly
        // once before any counter rows exist; here it must be tenant-scoped so it doesn't collide
        // with other tests' tenants that already have counter rows in this shared, un-reset
        // "toolbox_talks" schema - see the class-level remarks on Respawner's schema scope).
        //
        // ExecuteSqlRawAsync(sql, parameters) runs the sql through string.Format to substitute
        // {0}, so the regex quantifier "{4}" must be escaped as "{{4}}" here even though the
        // migration's own migrationBuilder.Sql(...) call executes the identical pattern literally
        // (no parameter substitution, so no escaping needed there).
        await seedDb.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO toolbox_talks."CertificateNumberCounters"
                ("Id", "TenantId", "Prefix", "Year", "LastNumber", "CreatedAt", "CreatedBy", "IsDeleted")
            SELECT
                gen_random_uuid(),
                parsed."TenantId",
                parsed."Prefix",
                parsed."Year",
                MAX(parsed."Seq"::int),
                now(),
                'system-migration',
                false
            FROM (
                SELECT
                    c."TenantId" AS "TenantId",
                    m[1] AS "Prefix",
                    (m[2])::int AS "Year",
                    m[3] AS "Seq"
                FROM toolbox_talks."ToolboxTalkCertificates" c,
                     LATERAL regexp_matches(c."CertificateNumber", '^(.+)-(\d{{4}})-(\d+)$') AS m
                WHERE c."CertificateNumber" IS NOT NULL
                  AND c."TenantId" = {0}
            ) parsed
            WHERE length(parsed."Prefix") <= 20
              AND length(parsed."Seq") <= 9
            GROUP BY parsed."TenantId", parsed."Prefix", parsed."Year";
            """,
            tenantId);

        var scheduledTalk = await SeedScheduledTalkAsync(seedDb, tenantId, employeeId);

        // Assert
        using var callScope = Factory.Services.CreateScope();
        var service = callScope.ServiceProvider.GetRequiredService<ICertificateGenerationService>();
        var result = await service.GenerateTalkCertificateAsync(scheduledTalk, null);

        result.Should().NotBeNull();
        result!.CertificateNumber.Should().Be($"LRN-{year}-000006",
            "the seeded counter must continue from the 5 pre-existing certificates rather than restarting at 1, " +
            "which would collide with the tenant's own existing LRN-{year}-000001 via the unique index");
    }
}
