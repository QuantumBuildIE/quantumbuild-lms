using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using QuantumBuild.Core.Application.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Frameworks;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Configuration;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;
using QuantumBuild.Tests.Common.TestTenant;
using QuantumBuild.Tests.Integration.Setup;
using QuantumBuild.Tests.Integration.Setup.Fakes;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Covers the fix where RequirementMappingJob's AI-candidate-selection query now consumes
/// IApplicableFrameworksService.GetTenantEntitlementsAsync instead of matching purely on
/// sector key. Previously a Standard whose profile happened to share a sector key with the
/// tenant's active sector could be surfaced as an AI mapping candidate regardless of whether
/// the tenant actually subscribed to that Standard.
///
/// The job's Claude call is intercepted via FakeAnthropicHttpMessageHandler, which returns an
/// empty suggestion array ("[]") and captures the outgoing prompt — assertions check which
/// requirement titles were included in the captured prompt (i.e. which requirements survived
/// LoadApprovedRequirementsAsync's candidate-selection query) without depending on Claude
/// actually suggesting anything or on any mapping being persisted.
///
/// Note: Respawner only resets the "public" schema, not "toolbox_talks" (where RegulatoryBody,
/// Sector, RegulatoryProfile, TenantSector and TenantStandardSubscription live), so rows persist
/// across test methods within a run — every test uses unique codes/keys/sectors/titles rather
/// than relying on a clean table.
/// </summary>
[Collection("Integration")]
public class RequirementMappingJobCandidateGatingTests : IntegrationTestBase
{
    public RequirementMappingJobCandidateGatingTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UniqueSuffix(string label) => $"{label}-{Guid.NewGuid():N}"[..30];

    private async Task<Sector> CreateSectorAsync(string label)
    {
        var context = GetDbContext();
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Key = $"rmj-{Guid.NewGuid():N}"[..20],
            Name = $"RMJ Test Sector {UniqueSuffix(label)}",
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
            Name = $"RMJ Regulation {UniqueSuffix(label)}",
            Code = $"REG{Guid.NewGuid():N}"[..15],
            Country = "IE",
            Kind = RegulatoryBodyKind.Regulation
        };
        var doc = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = $"RMJ Regulation Document {UniqueSuffix(label)}",
            Version = "1.0"
        };
        var profile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = doc.Id,
            SectorId = profileSector.Id,
            SectorKey = profileSector.Key,
            ScoreLabel = $"RMJ Score {UniqueSuffix(label)}",
            ExportLabel = $"RMJR{UniqueSuffix(label)}",
            Description = "Integration test profile",
            IsActive = true
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
            Name = $"RMJ Standard {UniqueSuffix(label)}",
            Code = $"STD{Guid.NewGuid():N}"[..15],
            Country = "International",
            Kind = RegulatoryBodyKind.Standard,
            SectorId = homeSector.Id
        };
        var doc = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = $"RMJ Standard Document {UniqueSuffix(label)}",
            Version = "1.0"
        };
        var profile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = doc.Id,
            SectorId = profileSector.Id,
            SectorKey = profileSector.Key,
            ScoreLabel = $"RMJ Standard Score {UniqueSuffix(label)}",
            ExportLabel = $"RMJS{UniqueSuffix(label)}",
            Description = "Integration test profile",
            IsActive = true
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
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateTalkWithSectionAsync(string label)
    {
        var talkId = Guid.NewGuid();
        var context = GetDbContext();
        context.Set<ToolboxTalk>().Add(new ToolboxTalk
        {
            Id = talkId,
            TenantId = TestTenantConstants.TenantId,
            Code = $"RMJ{Guid.NewGuid():N}"[..8],
            Title = $"RMJ Candidate Gating Talk {UniqueSuffix(label)}",
            Description = "Integration test talk for requirement-mapping candidate gating",
            Frequency = ToolboxTalkFrequency.Once,
            VideoSource = VideoSource.None,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = true,
            GenerateCertificate = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        context.Set<ToolboxTalkSection>().Add(new ToolboxTalkSection
        {
            Id = Guid.NewGuid(),
            ToolboxTalkId = talkId,
            SectionNumber = 1,
            Title = "Section 1",
            Content = "<p>Safety training content for candidate gating tests.</p>",
            RequiresAcknowledgment = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await context.SaveChangesAsync();
        return talkId;
    }

    /// <summary>
    /// Builds a RequirementMappingJob with fully controlled dependencies — real DbContext and
    /// IApplicableFrameworksService resolved from a fresh DI scope (so the entitlement query
    /// behaves exactly as it does in production), but the Claude HTTP call goes through
    /// FakeAnthropicHttpMessageHandler so no real network call is made and the outgoing prompt
    /// can be captured for assertions.
    /// </summary>
    private (RequirementMappingJob Job, FakeAnthropicHttpMessageHandler Handler) BuildJob(IServiceScope scope)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<IToolboxTalksDbContext>();
        var applicableFrameworksService = scope.ServiceProvider.GetRequiredService<IApplicableFrameworksService>();
        var aiUsageLogger = scope.ServiceProvider.GetRequiredService<IAiUsageLogger>();

        var handler = new FakeAnthropicHttpMessageHandler { ResponseContentText = "[]" };
        var httpClient = new HttpClient(handler);

        var settings = Options.Create(new SubtitleProcessingSettings
        {
            Claude = new QuantumBuild.Core.Application.Abstractions.AI.ClaudeSettings
            {
                BaseUrl = "https://fake-claude.test",
                ApiKey = "test-key"
            }
        });

        var aiProviders = Options.Create(new AIProviderOptions
        {
            Anthropic = new AnthropicProviderOptions
            {
                Models = new AnthropicModels { Sonnet = "claude-sonnet-test", Haiku = "claude-haiku-test" }
            }
        });

        var job = new RequirementMappingJob(
            dbContext,
            httpClient,
            settings,
            aiUsageLogger,
            applicableFrameworksService,
            NullLogger<RequirementMappingJob>.Instance,
            aiProviders);

        return (job, handler);
    }

    // ── tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Candidates_TenantWithSectorAppliedRegulation_IncludesRegulationRequirement()
    {
        var sector = await CreateSectorAsync(nameof(Candidates_TenantWithSectorAppliedRegulation_IncludesRegulationRequirement));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, profile) = await CreateRegulationChainAsync(sector, nameof(Candidates_TenantWithSectorAppliedRegulation_IncludesRegulationRequirement));
        var requirement = await CreateApprovedRequirementAsync(profile.Id, $"Regulation Req {UniqueSuffix("A")}");
        var talkId = await CreateTalkWithSectionAsync(nameof(Candidates_TenantWithSectorAppliedRegulation_IncludesRegulationRequirement));

        using var scope = Factory.Services.CreateScope();
        var (job, handler) = BuildJob(scope);
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        handler.CapturedRequestBody.Should().NotBeNull();
        handler.CapturedRequestBody.Should().Contain(requirement.Title);
        handler.CapturedRequestBody.Should().Contain(requirement.Id.ToString());
    }

    [Fact]
    public async Task Candidates_TenantWithSubscribedStandard_IncludesStandardRequirement()
    {
        var homeSector = await CreateSectorAsync(nameof(Candidates_TenantWithSubscribedStandard_IncludesStandardRequirement));
        var (standardBody, standardProfile) = await CreateStandardChainAsync(homeSector, homeSector,
            nameof(Candidates_TenantWithSubscribedStandard_IncludesStandardRequirement));
        var requirement = await CreateApprovedRequirementAsync(standardProfile.Id, $"Standard Req {UniqueSuffix("B")}");
        await SubscribeToStandardAsync(standardBody.Id);
        var talkId = await CreateTalkWithSectionAsync(nameof(Candidates_TenantWithSubscribedStandard_IncludesStandardRequirement));

        using var scope = Factory.Services.CreateScope();
        var (job, handler) = BuildJob(scope);
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        handler.CapturedRequestBody.Should().NotBeNull();
        handler.CapturedRequestBody.Should().Contain(requirement.Title);
        handler.CapturedRequestBody.Should().Contain(requirement.Id.ToString());
    }

    [Fact]
    public async Task Candidates_TenantWithBothRegulationAndSubscribedStandard_IncludesBoth()
    {
        var sector = await CreateSectorAsync(nameof(Candidates_TenantWithBothRegulationAndSubscribedStandard_IncludesBoth));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, regulationProfile) = await CreateRegulationChainAsync(sector,
            nameof(Candidates_TenantWithBothRegulationAndSubscribedStandard_IncludesBoth));
        var regulationRequirement = await CreateApprovedRequirementAsync(regulationProfile.Id, $"Both Regulation Req {UniqueSuffix("C")}");

        var standardHomeSector = await CreateSectorAsync(nameof(Candidates_TenantWithBothRegulationAndSubscribedStandard_IncludesBoth) + "-std");
        var (standardBody, standardProfile) = await CreateStandardChainAsync(standardHomeSector, standardHomeSector,
            nameof(Candidates_TenantWithBothRegulationAndSubscribedStandard_IncludesBoth));
        var standardRequirement = await CreateApprovedRequirementAsync(standardProfile.Id, $"Both Standard Req {UniqueSuffix("C")}");
        await SubscribeToStandardAsync(standardBody.Id);

        var talkId = await CreateTalkWithSectionAsync(nameof(Candidates_TenantWithBothRegulationAndSubscribedStandard_IncludesBoth));

        using var scope = Factory.Services.CreateScope();
        var (job, handler) = BuildJob(scope);
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        handler.CapturedRequestBody.Should().NotBeNull();
        handler.CapturedRequestBody.Should().Contain(regulationRequirement.Title);
        handler.CapturedRequestBody.Should().Contain(standardRequirement.Title);
    }

    [Fact]
    public async Task Candidates_TenantWithNeitherSectorNorSubscriptionReachingIt_ExcludesStandardRequirement()
    {
        // The Standard's own home sector and its profile's sector are never assigned to the
        // tenant, and the tenant never subscribes to this body — its requirement must never be
        // surfaced as an AI candidate. Note: the shared test tenant accumulates sector
        // assignments and subscriptions from other tests within the same run (Respawner does
        // not reset the toolbox_talks schema — see class doc comment), so this asserts the
        // requirement's absence from whatever prompt is captured rather than asserting no
        // prompt was captured at all.
        var homeSector = await CreateSectorAsync(nameof(Candidates_TenantWithNeitherSectorNorSubscriptionReachingIt_ExcludesStandardRequirement));
        var (_, standardProfile) = await CreateStandardChainAsync(homeSector, homeSector,
            nameof(Candidates_TenantWithNeitherSectorNorSubscriptionReachingIt_ExcludesStandardRequirement));
        var requirement = await CreateApprovedRequirementAsync(standardProfile.Id, $"Never Reachable Req {UniqueSuffix("D")}");
        // Deliberately not subscribing, no sector assignment either.

        var talkId = await CreateTalkWithSectionAsync(nameof(Candidates_TenantWithNeitherSectorNorSubscriptionReachingIt_ExcludesStandardRequirement));

        using var scope = Factory.Services.CreateScope();
        var (job, handler) = BuildJob(scope);
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        if (handler.CapturedRequestBody != null)
            handler.CapturedRequestBody.Should().NotContain(requirement.Title);
    }

    [Fact]
    public async Task Candidates_UnsubscribedStandardSharingTenantSector_IsExcluded_ThisIsTheBugTheFixCloses()
    {
        // A Standard's profile happens to target the tenant's own assigned sector, but the
        // tenant never subscribed to that Standard. Before the fix, the sector-only query
        // matched purely on RegulatoryProfile.SectorKey and would have surfaced this
        // requirement as an AI candidate regardless of subscription. After the fix, Standard
        // requirements require the Standard-kind subscription entitlement — sector overlap
        // alone is not enough.
        var sector = await CreateSectorAsync(nameof(Candidates_UnsubscribedStandardSharingTenantSector_IsExcluded_ThisIsTheBugTheFixCloses));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);

        var standardHomeSector = await CreateSectorAsync(
            nameof(Candidates_UnsubscribedStandardSharingTenantSector_IsExcluded_ThisIsTheBugTheFixCloses) + "-home");
        var (_, standardProfile) = await CreateStandardChainAsync(standardHomeSector, sector,
            nameof(Candidates_UnsubscribedStandardSharingTenantSector_IsExcluded_ThisIsTheBugTheFixCloses));
        var unsubscribedRequirement = await CreateApprovedRequirementAsync(
            standardProfile.Id, $"Sector Overlap Unsubscribed Req {UniqueSuffix("E")}");
        // Deliberately not subscribing to the Standard body.

        var talkId = await CreateTalkWithSectionAsync(nameof(Candidates_UnsubscribedStandardSharingTenantSector_IsExcluded_ThisIsTheBugTheFixCloses));

        using var scope = Factory.Services.CreateScope();
        var (job, handler) = BuildJob(scope);
        await job.MapRequirementsAsync(TestTenantConstants.TenantId, talkId, null);

        // The tenant's own sector assignment produced no Regulation-kind requirements (none were
        // created here), so with the fix applied the candidate set is empty and Claude is never
        // called — the previously-leaked Standard requirement must not appear in any captured prompt.
        if (handler.CapturedRequestBody != null)
            handler.CapturedRequestBody.Should().NotContain(unsubscribedRequirement.Title);
    }
}
