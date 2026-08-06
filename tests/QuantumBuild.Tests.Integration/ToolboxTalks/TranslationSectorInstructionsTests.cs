using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Frameworks;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.TestTenant;
using QuantumBuild.Tests.Integration.Setup;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Chunk 5 of the multi-standard regulatory feature: TranslationPrompts.GetSectorInstructions
/// no longer switches on a hardcoded sector key — it composes AI translation-prompt instructions
/// from IApplicableFrameworksService.GetTranslationInstructionsAsync, i.e. the tenant's actual
/// applicable frameworks (sector-applied Regulations + subscribed Standards) for the given sector.
/// Note: Respawner only resets the "public" schema, not "toolbox_talks" (where RegulatoryBody,
/// Sector, RegulatoryProfile, TenantSector and TenantStandardSubscription live), so rows persist
/// across test methods within a run — every test uses unique codes/keys/sectors rather than
/// relying on a clean table.
/// </summary>
[Collection("Integration")]
public class TranslationSectorInstructionsTests : IntegrationTestBase
{
    public TranslationSectorInstructionsTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── seeded-body text, ported verbatim from the removed TranslationPrompts.GetSectorInstructions switch ──

    private const string HiqaInstructions = """
        SECTOR-SPECIFIC REQUIREMENTS (Healthcare / Homecare — HIQA):
        - REGULATORY CONTEXT: This is a HIQA-regulated homecare/healthcare compliance document. Regulatory precision is mandatory.
        - SAFEGUARDING: "Safeguarding" must retain its full legal meaning — do not translate as generic "safety" or "protection".
        - MANDATORY REPORTING: Notification deadlines (e.g. "within 3 working days") must be numerically exact.
        - JOB TITLES: Designated Liaison Person (DLP), Person in Charge (PIC), Registered Provider — translate consistently using approved equivalents or retain in English if no standard equivalent exists.
        - CONSENT: "Informed consent", "capacity", "advocacy" — use clinical/legal standard translations.
        """;

    private const string HsaInstructions = """
        SECTOR-SPECIFIC REQUIREMENTS (Construction / Manufacturing — HSA):
        - REGULATORY CONTEXT: This is an HSA-regulated workplace safety document. Safety language precision is mandatory.
        - PPE: All personal protective equipment terms must be translated precisely — no omissions or paraphrasing.
        - PROHIBITIONS: "Do not", "never", "must not" must retain full imperative force — never soften to "should not" or "it is recommended".
        - ROLES: PSDP, PSCS, competent person — translate with approved equivalents only.
        - RISK: Hazard categories and risk levels must match source exactly.
        """;

    private const string FsaiInstructions = """
        SECTOR-SPECIFIC REQUIREMENTS (Food & Hospitality — FSAI):
        - REGULATORY CONTEXT: This is an FSAI-regulated food safety document. Allergen and HACCP terminology precision is mandatory.
        - ALLERGENS: All 14 declarable allergens must be named precisely — never paraphrased or approximated.
        - HACCP: Critical Control Point terminology must be consistent and exact throughout.
        - TEMPERATURES: All numeric temperature thresholds must be preserved exactly.
        - CCP LIMITS: Critical limits must not be softened or approximated.
        """;

    private const string RsaInstructions = """
        SECTOR-SPECIFIC REQUIREMENTS (Transport — RSA):
        - REGULATORY CONTEXT: This is an RSA-regulated road transport document. Numeric precision is mandatory.
        - DRIVER HOURS: All hour and rest period requirements must be numerically exact.
        - TACHOGRAPH: Technical terms must use approved translations only — do not improvise.
        - LOAD LIMITS: All weight and dimension limits must be preserved exactly.
        - PROHIBITIONS: Driving prohibitions must retain full legal force.
        """;

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UniqueSuffix(string label) => $"{label}-{Guid.NewGuid():N}"[..30];

    private async Task<Sector> CreateSectorAsync(string label)
    {
        var context = GetDbContext();
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Key = $"tsi-{Guid.NewGuid():N}"[..20],
            Name = $"TSI Test Sector {UniqueSuffix(label)}",
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
            IsDefault = false
        });
        await context.SaveChangesAsync();
    }

    private async Task<(RegulatoryBody Body, RegulatoryProfile Profile)> CreateRegulationChainAsync(
        Sector profileSector, string label, string? translationInstructions)
    {
        var context = GetDbContext();
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = $"TSI Regulation {UniqueSuffix(label)}",
            Code = $"REG{Guid.NewGuid():N}"[..15],
            Country = "IE",
            Kind = RegulatoryBodyKind.Regulation,
            TranslationInstructions = translationInstructions
        };
        var doc = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = $"TSI Regulation Document {UniqueSuffix(label)}",
            Version = "1.0"
        };
        var profile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = doc.Id,
            SectorId = profileSector.Id,
            SectorKey = profileSector.Key,
            ScoreLabel = $"TSI Score {UniqueSuffix(label)}",
            ExportLabel = $"TSIR{UniqueSuffix(label)}",
            Description = "Integration test profile"
        };
        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(doc);
        context.RegulatoryProfiles.Add(profile);
        await context.SaveChangesAsync();
        return (body, profile);
    }

    private async Task<(RegulatoryBody Body, RegulatoryProfile Profile)> CreateStandardChainAsync(
        Sector homeSector, Sector profileSector, string label, string? translationInstructions)
    {
        var context = GetDbContext();
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = $"TSI Standard {UniqueSuffix(label)}",
            Code = $"STD{Guid.NewGuid():N}"[..15],
            Country = "International",
            Kind = RegulatoryBodyKind.Standard,
            SectorId = homeSector.Id,
            TranslationInstructions = translationInstructions
        };
        var doc = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = $"TSI Standard Document {UniqueSuffix(label)}",
            Version = "1.0"
        };
        var profile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = doc.Id,
            SectorId = profileSector.Id,
            SectorKey = profileSector.Key,
            ScoreLabel = $"TSI Standard Score {UniqueSuffix(label)}",
            ExportLabel = $"TSIS{UniqueSuffix(label)}",
            Description = "Integration test profile"
        };
        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(doc);
        context.RegulatoryProfiles.Add(profile);
        await context.SaveChangesAsync();
        return (body, profile);
    }

    private async Task CreateApprovedRequirementAsync(Guid profileId, string title)
    {
        var context = GetDbContext();
        context.RegulatoryRequirements.Add(new RegulatoryRequirement
        {
            Id = Guid.NewGuid(),
            RegulatoryProfileId = profileId,
            Title = title,
            Description = "Integration test requirement",
            Priority = "med",
            IngestionStatus = RequirementIngestionStatus.Approved,
            IsActive = true
        });
        await context.SaveChangesAsync();
    }

    private async Task SubscribeToStandardAsync(Guid regulatoryBodyId)
    {
        var response = await AdminClient.PostAsync(
            $"/api/tenants/{TestTenantConstants.TenantId}/standards/{regulatoryBodyId}", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── GetTranslationInstructionsAsync ──────────────────────────────────────

    [Fact]
    public async Task GetTranslationInstructions_TenantWithOnlySectorAppliedRegulation_ReturnsRegulationInstructions()
    {
        var sector = await CreateSectorAsync(nameof(GetTranslationInstructions_TenantWithOnlySectorAppliedRegulation_ReturnsRegulationInstructions));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (body, profile) = await CreateRegulationChainAsync(
            sector, nameof(GetTranslationInstructions_TenantWithOnlySectorAppliedRegulation_ReturnsRegulationInstructions),
            "REGULATION TEXT FOR TEST");
        await CreateApprovedRequirementAsync(profile.Id, "Regulation requirement");

        var service = GetService<IApplicableFrameworksService>();
        var instructions = await service.GetTranslationInstructionsAsync(TestTenantConstants.TenantId, sector.Key);

        instructions.Should().Be("REGULATION TEXT FOR TEST");
    }

    [Fact]
    public async Task GetTranslationInstructions_TenantWithSubscribedStandard_ReturnsRegulationAndStandardInstructionsComposed()
    {
        var sector = await CreateSectorAsync(nameof(GetTranslationInstructions_TenantWithSubscribedStandard_ReturnsRegulationAndStandardInstructionsComposed));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (regBody, regProfile) = await CreateRegulationChainAsync(
            sector, nameof(GetTranslationInstructions_TenantWithSubscribedStandard_ReturnsRegulationAndStandardInstructionsComposed) + "-reg",
            "REGULATION BLOCK");
        await CreateApprovedRequirementAsync(regProfile.Id, "Regulation requirement");

        var (stdBody, stdProfile) = await CreateStandardChainAsync(
            sector, sector, nameof(GetTranslationInstructions_TenantWithSubscribedStandard_ReturnsRegulationAndStandardInstructionsComposed) + "-std",
            "STANDARD BLOCK");
        await CreateApprovedRequirementAsync(stdProfile.Id, "Standard requirement");
        await SubscribeToStandardAsync(stdBody.Id);

        var service = GetService<IApplicableFrameworksService>();
        var instructions = await service.GetTranslationInstructionsAsync(TestTenantConstants.TenantId, sector.Key);

        // Regulations sort before Standards (GetApplicableFrameworksAsync orders by Kind then BodyName) —
        // additive, not merged: both blocks present, Regulation first.
        instructions.Should().NotBeNull();
        instructions!.IndexOf("REGULATION BLOCK", StringComparison.Ordinal)
            .Should().BeLessThan(instructions.IndexOf("STANDARD BLOCK", StringComparison.Ordinal));
        instructions.Should().Contain("REGULATION BLOCK").And.Contain("STANDARD BLOCK");
    }

    [Fact]
    public async Task GetTranslationInstructions_NoApplicableFrameworksForSector_ReturnsNull()
    {
        // Sector exists but the tenant has neither the sector assigned nor any subscription
        // targeting it — mirrors the old switch's "unmatched sector" fallthrough to null.
        var sector = await CreateSectorAsync(nameof(GetTranslationInstructions_NoApplicableFrameworksForSector_ReturnsNull));

        var service = GetService<IApplicableFrameworksService>();
        var instructions = await service.GetTranslationInstructionsAsync(TestTenantConstants.TenantId, sector.Key);

        instructions.Should().BeNull();
    }

    [Fact]
    public async Task GetTranslationInstructions_NullOrEmptySectorKey_ReturnsNull()
    {
        var service = GetService<IApplicableFrameworksService>();

        (await service.GetTranslationInstructionsAsync(TestTenantConstants.TenantId, null)).Should().BeNull();
        (await service.GetTranslationInstructionsAsync(TestTenantConstants.TenantId, "")).Should().BeNull();
    }

    [Fact]
    public async Task GetTranslationInstructions_MultipleSubscribedStandardsForSameSector_ComposesAllBlocks()
    {
        var sector = await CreateSectorAsync(nameof(GetTranslationInstructions_MultipleSubscribedStandardsForSameSector_ComposesAllBlocks));

        var (stdBodyA, stdProfileA) = await CreateStandardChainAsync(
            sector, sector, nameof(GetTranslationInstructions_MultipleSubscribedStandardsForSameSector_ComposesAllBlocks) + "-a", "STANDARD A BLOCK");
        await CreateApprovedRequirementAsync(stdProfileA.Id, "Standard A requirement");
        await SubscribeToStandardAsync(stdBodyA.Id);

        var (stdBodyB, stdProfileB) = await CreateStandardChainAsync(
            sector, sector, nameof(GetTranslationInstructions_MultipleSubscribedStandardsForSameSector_ComposesAllBlocks) + "-b", "STANDARD B BLOCK");
        await CreateApprovedRequirementAsync(stdProfileB.Id, "Standard B requirement");
        await SubscribeToStandardAsync(stdBodyB.Id);

        var service = GetService<IApplicableFrameworksService>();
        var instructions = await service.GetTranslationInstructionsAsync(TestTenantConstants.TenantId, sector.Key);

        instructions.Should().Contain("STANDARD A BLOCK").And.Contain("STANDARD B BLOCK");
    }

    [Fact]
    public async Task GetTranslationInstructions_ApplicableFrameworkWithNoTranslationInstructions_FallsBackGracefully()
    {
        // A subscribed Standard with no TranslationInstructions text set (e.g. SuperUser hasn't
        // filled it in via the catalog UI yet) must not break composition — null case handled.
        var sector = await CreateSectorAsync(nameof(GetTranslationInstructions_ApplicableFrameworkWithNoTranslationInstructions_FallsBackGracefully));
        var (stdBody, stdProfile) = await CreateStandardChainAsync(
            sector, sector, nameof(GetTranslationInstructions_ApplicableFrameworkWithNoTranslationInstructions_FallsBackGracefully),
            translationInstructions: null);
        await CreateApprovedRequirementAsync(stdProfile.Id, "Standard requirement");
        await SubscribeToStandardAsync(stdBody.Id);

        var service = GetService<IApplicableFrameworksService>();
        var instructions = await service.GetTranslationInstructionsAsync(TestTenantConstants.TenantId, sector.Key);

        instructions.Should().BeNull();
    }

    // ── Seeded Regulations — verify no behavioural change from the old switch ──

    [Theory]
    [InlineData("HIQA", HiqaInstructions)]
    [InlineData("HSA", HsaInstructions)]
    [InlineData("FSAI", FsaiInstructions)]
    [InlineData("RSA", RsaInstructions)]
    public async Task SeededRegulationBody_HasTranslationInstructionsMatchingRemovedSwitchText(string code, string expected)
    {
        var context = GetDbContext();
        var body = await context.RegulatoryBodies
            .IgnoreQueryFilters()
            .SingleAsync(b => b.Code == code);

        body.TranslationInstructions.Should().Be(expected);
    }
}
