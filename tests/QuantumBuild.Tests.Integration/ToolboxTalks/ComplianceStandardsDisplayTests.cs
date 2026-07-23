using System.Net;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Frameworks;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.TestTenant;
using QuantumBuild.Tests.Integration.Setup;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Chunk 4 of the multi-standard regulatory feature: compliance display surfaces (checklist,
/// browse, applicable-frameworks summary) surface requirements from subscribed Standards
/// alongside sector-applied Regulations, each attributed to its source body/Kind.
/// Note: Respawner only resets the "public" schema, not "toolbox_talks" (where RegulatoryBody,
/// Sector, RegulatoryProfile, TenantSector and TenantStandardSubscription live), so rows persist
/// across test methods within a run — every test uses unique codes/keys/sectors rather than
/// relying on a clean table, and assertions are scoped to entities created within the test.
/// </summary>
[Collection("Integration")]
public class ComplianceStandardsDisplayTests : IntegrationTestBase
{
    public ComplianceStandardsDisplayTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UniqueSuffix(string label) => $"{label}-{Guid.NewGuid():N}"[..30];

    private async Task<Sector> CreateSectorAsync(string label)
    {
        var context = GetDbContext();
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Key = $"csd-{Guid.NewGuid():N}"[..20],
            Name = $"CSD Test Sector {UniqueSuffix(label)}",
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
        Sector profileSector, string label)
    {
        var context = GetDbContext();
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = $"CSD Regulation {UniqueSuffix(label)}",
            Code = $"REG{Guid.NewGuid():N}"[..15],
            Country = "IE",
            Kind = RegulatoryBodyKind.Regulation
        };
        var doc = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = $"CSD Regulation Document {UniqueSuffix(label)}",
            Version = "1.0"
        };
        var profile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = doc.Id,
            SectorId = profileSector.Id,
            SectorKey = profileSector.Key,
            ScoreLabel = $"CSD Score {UniqueSuffix(label)}",
            ExportLabel = $"CSDR{UniqueSuffix(label)}",
            Description = "Integration test profile"
        };
        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(doc);
        context.RegulatoryProfiles.Add(profile);
        await context.SaveChangesAsync();
        return (body, profile);
    }

    private async Task<(RegulatoryBody Body, RegulatoryProfile Profile)> CreateStandardChainAsync(
        Sector homeSector, Sector profileSector, string label)
    {
        var context = GetDbContext();
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = $"CSD Standard {UniqueSuffix(label)}",
            Code = $"STD{Guid.NewGuid():N}"[..15],
            Country = "International",
            Kind = RegulatoryBodyKind.Standard,
            SectorId = homeSector.Id
        };
        var doc = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = $"CSD Standard Document {UniqueSuffix(label)}",
            Version = "1.0"
        };
        var profile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = doc.Id,
            SectorId = profileSector.Id,
            SectorKey = profileSector.Key,
            ScoreLabel = $"CSD Standard Score {UniqueSuffix(label)}",
            ExportLabel = $"CSDS{UniqueSuffix(label)}",
            Description = "Integration test profile"
        };
        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(doc);
        context.RegulatoryProfiles.Add(profile);
        await context.SaveChangesAsync();
        return (body, profile);
    }

    private async Task<RegulatoryRequirement> CreateApprovedRequirementAsync(Guid profileId, string title)
    {
        var context = GetDbContext();
        var req = new RegulatoryRequirement
        {
            Id = Guid.NewGuid(),
            RegulatoryProfileId = profileId,
            Title = title,
            Description = "Integration test requirement",
            Priority = "med",
            IngestionStatus = RequirementIngestionStatus.Approved,
            IsActive = true
        };
        context.RegulatoryRequirements.Add(req);
        await context.SaveChangesAsync();
        return req;
    }

    private async Task SubscribeToStandardAsync(Guid regulatoryBodyId)
    {
        var response = await AdminClient.PostAsync(
            $"/api/tenants/{TestTenantConstants.TenantId}/standards/{regulatoryBodyId}", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Applicable Frameworks ────────────────────────────────────────────────

    [Fact]
    public async Task ApplicableFrameworks_SectorLinkedRegulation_IsIncludedAsRegulation()
    {
        var sector = await CreateSectorAsync(nameof(ApplicableFrameworks_SectorLinkedRegulation_IsIncludedAsRegulation));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (body, profile) = await CreateRegulationChainAsync(sector, nameof(ApplicableFrameworks_SectorLinkedRegulation_IsIncludedAsRegulation));
        await CreateApprovedRequirementAsync(profile.Id, "Regulation requirement");

        var (response, frameworks) = await AdminClient.GetWithResponseAsync<List<ApplicableFrameworkDto>>(
            "/api/toolbox-talks/requirement-mappings/applicable-frameworks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = frameworks!.Single(f => f.RegulatoryBodyId == body.Id);
        entry.Kind.Should().Be("Regulation");
        entry.Source.Should().Be("Sector");
        entry.SectorKey.Should().Be(sector.Key);
    }

    [Fact]
    public async Task ApplicableFrameworks_SubscribedStandard_IsIncludedAsStandard_WithNoRegulationEntryForIt()
    {
        var homeSector = await CreateSectorAsync(nameof(ApplicableFrameworks_SubscribedStandard_IsIncludedAsStandard_WithNoRegulationEntryForIt));
        var (body, profile) = await CreateStandardChainAsync(homeSector, homeSector,
            nameof(ApplicableFrameworks_SubscribedStandard_IsIncludedAsStandard_WithNoRegulationEntryForIt));
        await CreateApprovedRequirementAsync(profile.Id, "Standard requirement");
        await SubscribeToStandardAsync(body.Id);

        var (response, frameworks) = await AdminClient.GetWithResponseAsync<List<ApplicableFrameworkDto>>(
            "/api/toolbox-talks/requirement-mappings/applicable-frameworks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = frameworks!.Single(f => f.RegulatoryBodyId == body.Id);
        entry.Kind.Should().Be("Standard");
        entry.Source.Should().Be("Subscription");
        entry.ApprovedRequirementCount.Should().Be(1);

        // The same body never appears twice under a different Kind/Source.
        frameworks!.Count(f => f.RegulatoryBodyId == body.Id).Should().Be(1);
    }

    [Fact]
    public async Task ApplicableFrameworks_BothRegulationAndSubscribedStandard_IncludesBoth()
    {
        var sector = await CreateSectorAsync(nameof(ApplicableFrameworks_BothRegulationAndSubscribedStandard_IncludesBoth));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (regulationBody, regulationProfile) = await CreateRegulationChainAsync(sector,
            nameof(ApplicableFrameworks_BothRegulationAndSubscribedStandard_IncludesBoth));
        await CreateApprovedRequirementAsync(regulationProfile.Id, "Regulation requirement");

        var standardHomeSector = await CreateSectorAsync(nameof(ApplicableFrameworks_BothRegulationAndSubscribedStandard_IncludesBoth) + "-std");
        var (standardBody, standardProfile) = await CreateStandardChainAsync(standardHomeSector, standardHomeSector,
            nameof(ApplicableFrameworks_BothRegulationAndSubscribedStandard_IncludesBoth));
        await CreateApprovedRequirementAsync(standardProfile.Id, "Standard requirement");
        await SubscribeToStandardAsync(standardBody.Id);

        var (response, frameworks) = await AdminClient.GetWithResponseAsync<List<ApplicableFrameworkDto>>(
            "/api/toolbox-talks/requirement-mappings/applicable-frameworks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        frameworks!.Should().Contain(f => f.RegulatoryBodyId == regulationBody.Id && f.Kind == "Regulation");
        frameworks!.Should().Contain(f => f.RegulatoryBodyId == standardBody.Id && f.Kind == "Standard");
    }

    // ── Compliance checklist ─────────────────────────────────────────────────

    [Fact]
    public async Task ComplianceChecklist_MixesRegulationAndSubscribedStandard_WithSourceAttributionPerRequirement()
    {
        var sector = await CreateSectorAsync(nameof(ComplianceChecklist_MixesRegulationAndSubscribedStandard_WithSourceAttributionPerRequirement));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);

        var (regulationBody, regulationProfile) = await CreateRegulationChainAsync(sector,
            nameof(ComplianceChecklist_MixesRegulationAndSubscribedStandard_WithSourceAttributionPerRequirement));
        var regulationRequirement = await CreateApprovedRequirementAsync(regulationProfile.Id, "Regulation-sourced requirement");

        // Standard's own "home" sector differs from the tenant sector its profile targets —
        // subscription is the entitlement, sector alignment is incidental.
        var standardHomeSector = await CreateSectorAsync(
            nameof(ComplianceChecklist_MixesRegulationAndSubscribedStandard_WithSourceAttributionPerRequirement) + "-home");
        var (standardBody, standardProfile) = await CreateStandardChainAsync(standardHomeSector, sector,
            nameof(ComplianceChecklist_MixesRegulationAndSubscribedStandard_WithSourceAttributionPerRequirement));
        var standardRequirement = await CreateApprovedRequirementAsync(standardProfile.Id, "Standard-sourced requirement");
        await SubscribeToStandardAsync(standardBody.Id);

        var (response, checklist) = await AdminClient.GetWithResponseAsync<ComplianceChecklistDto>(
            $"/api/toolbox-talks/requirement-mappings/compliance/{sector.Key}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var allRequirements = checklist!.PrincipleGroups.SelectMany(g => g.Requirements).ToList();

        var regDto = allRequirements.Single(r => r.Id == regulationRequirement.Id);
        regDto.SourceBodyKind.Should().Be("Regulation");
        regDto.SourceBodyName.Should().Be(regulationBody.Name);

        var stdDto = allRequirements.Single(r => r.Id == standardRequirement.Id);
        stdDto.SourceBodyKind.Should().Be("Standard");
        stdDto.SourceBodyName.Should().Be(standardBody.Name);

        checklist.RegulatoryBody.Should().Contain(regulationBody.Name);
        checklist.RegulatoryBody.Should().Contain(standardBody.Name);
    }

    [Fact]
    public async Task ComplianceChecklist_ExcludesRequirementFromUnsubscribedStandard_EvenInTenantsOwnSector()
    {
        var sector = await CreateSectorAsync(nameof(ComplianceChecklist_ExcludesRequirementFromUnsubscribedStandard_EvenInTenantsOwnSector));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);

        // A Standard's profile happens to target the tenant's own sector, but the tenant never
        // subscribed — its requirement must not leak in just because the sector key matches.
        var standardHomeSector = await CreateSectorAsync(
            nameof(ComplianceChecklist_ExcludesRequirementFromUnsubscribedStandard_EvenInTenantsOwnSector) + "-home");
        var (_, standardProfile) = await CreateStandardChainAsync(standardHomeSector, sector,
            nameof(ComplianceChecklist_ExcludesRequirementFromUnsubscribedStandard_EvenInTenantsOwnSector));
        var unsubscribedRequirement = await CreateApprovedRequirementAsync(standardProfile.Id, "Unsubscribed standard requirement");

        var (response, checklist) = await AdminClient.GetWithResponseAsync<ComplianceChecklistDto>(
            $"/api/toolbox-talks/requirement-mappings/compliance/{sector.Key}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var allRequirements = checklist!.PrincipleGroups.SelectMany(g => g.Requirements).ToList();
        allRequirements.Should().NotContain(r => r.Id == unsubscribedRequirement.Id);
    }

    [Fact]
    public async Task ComplianceChecklist_ReachableForSubscriptionOnlySector_NotAssignedToTenant()
    {
        // Tenant has no TenantSector for this sector at all — only a Standard subscription
        // whose profile targets it. The relaxed guard must still allow the tab to render.
        var subscriptionOnlySector = await CreateSectorAsync(
            nameof(ComplianceChecklist_ReachableForSubscriptionOnlySector_NotAssignedToTenant));
        var (standardBody, standardProfile) = await CreateStandardChainAsync(subscriptionOnlySector, subscriptionOnlySector,
            nameof(ComplianceChecklist_ReachableForSubscriptionOnlySector_NotAssignedToTenant));
        var requirement = await CreateApprovedRequirementAsync(standardProfile.Id, "Cross-sector standard requirement");
        await SubscribeToStandardAsync(standardBody.Id);

        var (response, checklist) = await AdminClient.GetWithResponseAsync<ComplianceChecklistDto>(
            $"/api/toolbox-talks/requirement-mappings/compliance/{subscriptionOnlySector.Key}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var allRequirements = checklist!.PrincipleGroups.SelectMany(g => g.Requirements).ToList();
        allRequirements.Should().Contain(r => r.Id == requirement.Id);
    }

    [Fact]
    public async Task ComplianceChecklist_UnassignedUnsubscribedSector_Returns403()
    {
        var unreachableSector = await CreateSectorAsync(nameof(ComplianceChecklist_UnassignedUnsubscribedSector_Returns403));

        var response = await AdminClient.GetAsync(
            $"/api/toolbox-talks/requirement-mappings/compliance/{unreachableSector.Key}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Regulatory browse ────────────────────────────────────────────────────

    [Fact]
    public async Task Browse_IncludesSubscribedStandardRequirement_WithStandardKind()
    {
        var homeSector = await CreateSectorAsync(nameof(Browse_IncludesSubscribedStandardRequirement_WithStandardKind));
        var (standardBody, standardProfile) = await CreateStandardChainAsync(homeSector, homeSector,
            nameof(Browse_IncludesSubscribedStandardRequirement_WithStandardKind));
        var requirement = await CreateApprovedRequirementAsync(standardProfile.Id, "Browsable standard requirement");
        await SubscribeToStandardAsync(standardBody.Id);

        var (response, bodies) = await AdminClient.GetWithResponseAsync<List<RegulatoryBrowseBodyDto>>("/api/regulatory/browse");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyDto = bodies!.Single(b => b.Id == standardBody.Id);
        bodyDto.Kind.Should().Be("Standard");
        bodyDto.Documents.SelectMany(d => d.PrincipleGroups).SelectMany(g => g.Requirements)
            .Should().Contain(r => r.Id == requirement.Id);
    }

    [Fact]
    public async Task Browse_ExcludesRequirementFromUnsubscribedStandard()
    {
        var homeSector = await CreateSectorAsync(nameof(Browse_ExcludesRequirementFromUnsubscribedStandard));
        var (standardBody, standardProfile) = await CreateStandardChainAsync(homeSector, homeSector,
            nameof(Browse_ExcludesRequirementFromUnsubscribedStandard));
        await CreateApprovedRequirementAsync(standardProfile.Id, "Never subscribed requirement");
        // Deliberately not subscribing.

        var (response, bodies) = await AdminClient.GetWithResponseAsync<List<RegulatoryBrowseBodyDto>>("/api/regulatory/browse");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        bodies!.Should().NotContain(b => b.Id == standardBody.Id);
    }

    [Fact]
    public async Task Browse_IncludesSectorLinkedRegulation_WithRegulationKind()
    {
        var sector = await CreateSectorAsync(nameof(Browse_IncludesSectorLinkedRegulation_WithRegulationKind));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (body, profile) = await CreateRegulationChainAsync(sector, nameof(Browse_IncludesSectorLinkedRegulation_WithRegulationKind));
        var requirement = await CreateApprovedRequirementAsync(profile.Id, "Browsable regulation requirement");

        var (response, bodies) = await AdminClient.GetWithResponseAsync<List<RegulatoryBrowseBodyDto>>("/api/regulatory/browse");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var bodyDto = bodies!.Single(b => b.Id == body.Id);
        bodyDto.Kind.Should().Be("Regulation");
        bodyDto.Documents.SelectMany(d => d.PrincipleGroups).SelectMany(g => g.Requirements)
            .Should().Contain(r => r.Id == requirement.Id);
    }
}
