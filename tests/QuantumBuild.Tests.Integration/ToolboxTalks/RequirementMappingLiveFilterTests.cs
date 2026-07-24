using System.Net;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Tests.Common.TestTenant;
using QuantumBuild.Tests.Integration.Setup;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Covers the "live learnings only" fix: regulatory mapping surfaces must only treat a talk
/// (Published + Active + not deleted) or course (Active + not deleted) as a valid compliance
/// target. Compliance checklist / Evidence Pack hard-exclude non-live mappings; the Mappings
/// tab (pending) and bulk confirm-all badge/skip them instead of hiding.
///
/// Note: Respawner only resets the "public" schema, not "toolbox_talks" (where RegulatoryBody,
/// Sector, RegulatoryProfile, RegulatoryRequirement and RegulatoryRequirementMapping live), so
/// rows persist across test methods within a run — every test uses unique codes/keys/titles and
/// scopes assertions to entities created within the test, per the existing convention in
/// ComplianceStandardsDisplayTests and RequirementMappingJobCandidateGatingTests.
/// </summary>
[Collection("Integration")]
public class RequirementMappingLiveFilterTests : IntegrationTestBase
{
    public RequirementMappingLiveFilterTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static string UniqueSuffix(string label) => $"{label}-{Guid.NewGuid():N}"[..30];

    private async Task<Sector> CreateSectorAsync(string label)
    {
        var context = GetDbContext();
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Key = $"rmlf-{Guid.NewGuid():N}"[..20],
            Name = $"RMLF Test Sector {UniqueSuffix(label)}",
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
            Name = $"RMLF Regulation {UniqueSuffix(label)}",
            Code = $"REG{Guid.NewGuid():N}"[..15],
            Country = "IE",
            Kind = RegulatoryBodyKind.Regulation
        };
        var doc = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = $"RMLF Regulation Document {UniqueSuffix(label)}",
            Version = "1.0"
        };
        var profile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = doc.Id,
            SectorId = profileSector.Id,
            SectorKey = profileSector.Key,
            ScoreLabel = $"RMLF Score {UniqueSuffix(label)}",
            ExportLabel = $"RMLFR{UniqueSuffix(label)}",
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

    private async Task<Guid> CreateTalkAsync(
        string label, ToolboxTalkStatus status, bool isActive, bool isDeleted = false)
    {
        var talkId = Guid.NewGuid();
        var context = GetDbContext();
        var talk = new ToolboxTalk
        {
            Id = talkId,
            TenantId = TestTenantConstants.TenantId,
            Code = $"RMLF{Guid.NewGuid():N}"[..8],
            Title = $"RMLF Talk {UniqueSuffix(label)}",
            Description = "Integration test talk for live-filter tests",
            Frequency = ToolboxTalkFrequency.Once,
            VideoSource = VideoSource.None,
            MinimumVideoWatchPercent = 90,
            RequiresQuiz = false,
            IsActive = isActive,
            Status = status,
            GenerateCertificate = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        context.ToolboxTalks.Add(talk);
        await context.SaveChangesAsync();

        if (isDeleted)
        {
            // ApplicationDbContext.SetAuditFields forces IsDeleted = false on Added entities,
            // so soft-deletion must be a separate Modified-state save after the initial insert.
            talk.IsDeleted = true;
            context.Entry(talk).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        return talkId;
    }

    private async Task<Guid> CreateCourseAsync(string label, bool isActive, bool isDeleted = false)
    {
        var courseId = Guid.NewGuid();
        var context = GetDbContext();
        var course = new ToolboxTalkCourse
        {
            Id = courseId,
            TenantId = TestTenantConstants.TenantId,
            Title = $"RMLF Course {UniqueSuffix(label)}",
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        context.ToolboxTalkCourses.Add(course);
        await context.SaveChangesAsync();

        if (isDeleted)
        {
            course.IsDeleted = true;
            context.Entry(course).State = EntityState.Modified;
            await context.SaveChangesAsync();
        }

        return courseId;
    }

    private async Task<Guid> CreateMappingAsync(
        Guid requirementId, Guid? talkId, Guid? courseId, RequirementMappingStatus status)
    {
        var mappingId = Guid.NewGuid();
        var context = GetDbContext();
        context.RegulatoryRequirementMappings.Add(new RegulatoryRequirementMapping
        {
            Id = mappingId,
            TenantId = TestTenantConstants.TenantId,
            RegulatoryRequirementId = requirementId,
            ToolboxTalkId = talkId,
            CourseId = courseId,
            MappingStatus = status,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        });
        await context.SaveChangesAsync();
        return mappingId;
    }

    private async Task<RegulatoryRequirementMapping?> GetMappingAsync(Guid mappingId)
    {
        var context = GetDbContext();
        return await context.RegulatoryRequirementMappings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == mappingId);
    }

    private record ConfirmAllResponseDto(int Confirmed);

    // ── Compliance checklist — hard exclude ─────────────────────────────────

    [Fact]
    public async Task ComplianceChecklist_MappingToPublishedActiveTalk_Appears()
    {
        var sector = await CreateSectorAsync(nameof(ComplianceChecklist_MappingToPublishedActiveTalk_Appears));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, profile) = await CreateRegulationChainAsync(sector, nameof(ComplianceChecklist_MappingToPublishedActiveTalk_Appears));
        var requirement = await CreateApprovedRequirementAsync(profile.Id, $"Live talk requirement {UniqueSuffix("A")}");
        var talkId = await CreateTalkAsync(nameof(ComplianceChecklist_MappingToPublishedActiveTalk_Appears),
            ToolboxTalkStatus.Published, isActive: true);
        await CreateMappingAsync(requirement.Id, talkId, null, RequirementMappingStatus.Confirmed);

        var (response, checklist) = await AdminClient.GetWithResponseAsync<ComplianceChecklistDto>(
            $"/api/toolbox-talks/requirement-mappings/compliance/{sector.Key}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reqDto = checklist!.PrincipleGroups.SelectMany(g => g.Requirements).Single(r => r.Id == requirement.Id);
        reqDto.Mappings.Should().ContainSingle(m => m.ContentId == talkId);
        reqDto.CoverageStatus.Should().Be("Pending"); // Confirmed but no validation run yet
    }

    [Fact]
    public async Task ComplianceChecklist_MappingToDraftTalk_DoesNotAppear()
    {
        var sector = await CreateSectorAsync(nameof(ComplianceChecklist_MappingToDraftTalk_DoesNotAppear));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, profile) = await CreateRegulationChainAsync(sector, nameof(ComplianceChecklist_MappingToDraftTalk_DoesNotAppear));
        var requirement = await CreateApprovedRequirementAsync(profile.Id, $"Draft talk requirement {UniqueSuffix("B")}");
        var talkId = await CreateTalkAsync(nameof(ComplianceChecklist_MappingToDraftTalk_DoesNotAppear),
            ToolboxTalkStatus.Draft, isActive: true);
        await CreateMappingAsync(requirement.Id, talkId, null, RequirementMappingStatus.Confirmed);

        var (response, checklist) = await AdminClient.GetWithResponseAsync<ComplianceChecklistDto>(
            $"/api/toolbox-talks/requirement-mappings/compliance/{sector.Key}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reqDto = checklist!.PrincipleGroups.SelectMany(g => g.Requirements).Single(r => r.Id == requirement.Id);
        reqDto.Mappings.Should().BeEmpty();
        reqDto.CoverageStatus.Should().Be("Gap");
    }

    [Fact]
    public async Task ComplianceChecklist_MappingToPublishedDeactivatedTalk_DoesNotAppear()
    {
        var sector = await CreateSectorAsync(nameof(ComplianceChecklist_MappingToPublishedDeactivatedTalk_DoesNotAppear));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, profile) = await CreateRegulationChainAsync(sector, nameof(ComplianceChecklist_MappingToPublishedDeactivatedTalk_DoesNotAppear));
        var requirement = await CreateApprovedRequirementAsync(profile.Id, $"Deactivated talk requirement {UniqueSuffix("C")}");
        var talkId = await CreateTalkAsync(nameof(ComplianceChecklist_MappingToPublishedDeactivatedTalk_DoesNotAppear),
            ToolboxTalkStatus.Published, isActive: false);
        await CreateMappingAsync(requirement.Id, talkId, null, RequirementMappingStatus.Confirmed);

        var (response, checklist) = await AdminClient.GetWithResponseAsync<ComplianceChecklistDto>(
            $"/api/toolbox-talks/requirement-mappings/compliance/{sector.Key}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reqDto = checklist!.PrincipleGroups.SelectMany(g => g.Requirements).Single(r => r.Id == requirement.Id);
        reqDto.Mappings.Should().BeEmpty();
        reqDto.CoverageStatus.Should().Be("Gap");
    }

    [Fact]
    public async Task ComplianceChecklist_MappingToSoftDeletedTalk_DoesNotAppear()
    {
        var sector = await CreateSectorAsync(nameof(ComplianceChecklist_MappingToSoftDeletedTalk_DoesNotAppear));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, profile) = await CreateRegulationChainAsync(sector, nameof(ComplianceChecklist_MappingToSoftDeletedTalk_DoesNotAppear));
        var requirement = await CreateApprovedRequirementAsync(profile.Id, $"Deleted talk requirement {UniqueSuffix("D")}");
        var talkId = await CreateTalkAsync(nameof(ComplianceChecklist_MappingToSoftDeletedTalk_DoesNotAppear),
            ToolboxTalkStatus.Published, isActive: true, isDeleted: true);
        await CreateMappingAsync(requirement.Id, talkId, null, RequirementMappingStatus.Confirmed);

        var (response, checklist) = await AdminClient.GetWithResponseAsync<ComplianceChecklistDto>(
            $"/api/toolbox-talks/requirement-mappings/compliance/{sector.Key}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reqDto = checklist!.PrincipleGroups.SelectMany(g => g.Requirements).Single(r => r.Id == requirement.Id);
        reqDto.Mappings.Should().BeEmpty();
        reqDto.CoverageStatus.Should().Be("Gap");
    }

    [Fact]
    public async Task ComplianceChecklist_MappingToActiveCourse_Appears()
    {
        var sector = await CreateSectorAsync(nameof(ComplianceChecklist_MappingToActiveCourse_Appears));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, profile) = await CreateRegulationChainAsync(sector, nameof(ComplianceChecklist_MappingToActiveCourse_Appears));
        var requirement = await CreateApprovedRequirementAsync(profile.Id, $"Live course requirement {UniqueSuffix("E")}");
        var courseId = await CreateCourseAsync(nameof(ComplianceChecklist_MappingToActiveCourse_Appears), isActive: true);
        await CreateMappingAsync(requirement.Id, null, courseId, RequirementMappingStatus.Confirmed);

        var (response, checklist) = await AdminClient.GetWithResponseAsync<ComplianceChecklistDto>(
            $"/api/toolbox-talks/requirement-mappings/compliance/{sector.Key}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reqDto = checklist!.PrincipleGroups.SelectMany(g => g.Requirements).Single(r => r.Id == requirement.Id);
        reqDto.Mappings.Should().ContainSingle(m => m.ContentId == courseId);
        reqDto.CoverageStatus.Should().Be("Pending"); // Confirmed but no validation run yet
    }

    [Fact]
    public async Task ComplianceChecklist_MappingToDeactivatedCourse_DoesNotAppear()
    {
        var sector = await CreateSectorAsync(nameof(ComplianceChecklist_MappingToDeactivatedCourse_DoesNotAppear));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, profile) = await CreateRegulationChainAsync(sector, nameof(ComplianceChecklist_MappingToDeactivatedCourse_DoesNotAppear));
        var requirement = await CreateApprovedRequirementAsync(profile.Id, $"Deactivated course requirement {UniqueSuffix("F")}");
        var courseId = await CreateCourseAsync(nameof(ComplianceChecklist_MappingToDeactivatedCourse_DoesNotAppear), isActive: false);
        await CreateMappingAsync(requirement.Id, null, courseId, RequirementMappingStatus.Confirmed);

        var (response, checklist) = await AdminClient.GetWithResponseAsync<ComplianceChecklistDto>(
            $"/api/toolbox-talks/requirement-mappings/compliance/{sector.Key}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var reqDto = checklist!.PrincipleGroups.SelectMany(g => g.Requirements).Single(r => r.Id == requirement.Id);
        reqDto.Mappings.Should().BeEmpty();
        reqDto.CoverageStatus.Should().Be("Gap");
    }

    // ── Pending mappings — badge, not hide ──────────────────────────────────

    [Fact]
    public async Task PendingMappings_MappingToDraftTalk_AppearsWithTargetIsLiveFalse()
    {
        var sector = await CreateSectorAsync(nameof(PendingMappings_MappingToDraftTalk_AppearsWithTargetIsLiveFalse));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, profile) = await CreateRegulationChainAsync(sector, nameof(PendingMappings_MappingToDraftTalk_AppearsWithTargetIsLiveFalse));
        var requirement = await CreateApprovedRequirementAsync(profile.Id, $"Pending draft talk requirement {UniqueSuffix("G")}");
        var talkId = await CreateTalkAsync(nameof(PendingMappings_MappingToDraftTalk_AppearsWithTargetIsLiveFalse),
            ToolboxTalkStatus.Draft, isActive: true);
        var mappingId = await CreateMappingAsync(requirement.Id, talkId, null, RequirementMappingStatus.Suggested);

        var (response, summary) = await AdminClient.GetWithResponseAsync<MappingSummaryDto>(
            "/api/toolbox-talks/requirement-mappings/pending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = summary!.PendingReview.Single(m => m.Id == mappingId);
        dto.ContentId.Should().Be(talkId);
        dto.TargetIsLive.Should().BeFalse();
    }

    [Fact]
    public async Task PendingMappings_MappingToPublishedActiveTalk_AppearsWithTargetIsLiveTrue()
    {
        var sector = await CreateSectorAsync(nameof(PendingMappings_MappingToPublishedActiveTalk_AppearsWithTargetIsLiveTrue));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, profile) = await CreateRegulationChainAsync(sector, nameof(PendingMappings_MappingToPublishedActiveTalk_AppearsWithTargetIsLiveTrue));
        var requirement = await CreateApprovedRequirementAsync(profile.Id, $"Pending live talk requirement {UniqueSuffix("H")}");
        var talkId = await CreateTalkAsync(nameof(PendingMappings_MappingToPublishedActiveTalk_AppearsWithTargetIsLiveTrue),
            ToolboxTalkStatus.Published, isActive: true);
        var mappingId = await CreateMappingAsync(requirement.Id, talkId, null, RequirementMappingStatus.Suggested);

        var (response, summary) = await AdminClient.GetWithResponseAsync<MappingSummaryDto>(
            "/api/toolbox-talks/requirement-mappings/pending");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = summary!.PendingReview.Single(m => m.Id == mappingId);
        dto.ContentId.Should().Be(talkId);
        dto.TargetIsLive.Should().BeTrue();
    }

    // ── Bulk confirm-all — exclude non-live from scope ──────────────────────

    [Fact]
    public async Task ConfirmAll_ConfirmsLiveMapping_LeavesNonLiveMappingSuggested()
    {
        var sector = await CreateSectorAsync(nameof(ConfirmAll_ConfirmsLiveMapping_LeavesNonLiveMappingSuggested));
        await AssignSectorToTenantAsync(TestTenantConstants.TenantId, sector.Id);
        var (_, profile) = await CreateRegulationChainAsync(sector, nameof(ConfirmAll_ConfirmsLiveMapping_LeavesNonLiveMappingSuggested));

        var liveRequirement = await CreateApprovedRequirementAsync(profile.Id, $"Bulk live requirement {UniqueSuffix("I")}");
        var liveTalkId = await CreateTalkAsync(nameof(ConfirmAll_ConfirmsLiveMapping_LeavesNonLiveMappingSuggested) + "-live",
            ToolboxTalkStatus.Published, isActive: true);
        var liveMappingId = await CreateMappingAsync(liveRequirement.Id, liveTalkId, null, RequirementMappingStatus.Suggested);

        var nonLiveRequirement = await CreateApprovedRequirementAsync(profile.Id, $"Bulk non-live requirement {UniqueSuffix("I")}");
        var draftTalkId = await CreateTalkAsync(nameof(ConfirmAll_ConfirmsLiveMapping_LeavesNonLiveMappingSuggested) + "-draft",
            ToolboxTalkStatus.Draft, isActive: true);
        var nonLiveMappingId = await CreateMappingAsync(nonLiveRequirement.Id, draftTalkId, null, RequirementMappingStatus.Suggested);

        var (response, result) = await AdminClient.PostWithResponseAsync<object?, ConfirmAllResponseDto>(
            "/api/toolbox-talks/requirement-mappings/confirm-all", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Confirmed.Should().BeGreaterThanOrEqualTo(1);

        var liveMapping = await GetMappingAsync(liveMappingId);
        liveMapping!.MappingStatus.Should().Be(RequirementMappingStatus.Confirmed);

        var nonLiveMapping = await GetMappingAsync(nonLiveMappingId);
        nonLiveMapping!.MappingStatus.Should().Be(RequirementMappingStatus.Suggested);
    }

    // ── Content options — manual-mapping target picker ──────────────────────

    [Fact]
    public async Task ContentOptions_DraftTalk_NotReturned()
    {
        var talkId = await CreateTalkAsync(nameof(ContentOptions_DraftTalk_NotReturned), ToolboxTalkStatus.Draft, isActive: true);

        var (response, options) = await AdminClient.GetWithResponseAsync<List<ContentOptionDto>>(
            "/api/toolbox-talks/requirement-mappings/content-options");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        options!.Should().NotContain(o => o.Id == talkId);
    }

    [Fact]
    public async Task ContentOptions_PublishedDeactivatedTalk_NotReturned()
    {
        var talkId = await CreateTalkAsync(nameof(ContentOptions_PublishedDeactivatedTalk_NotReturned), ToolboxTalkStatus.Published, isActive: false);

        var (response, options) = await AdminClient.GetWithResponseAsync<List<ContentOptionDto>>(
            "/api/toolbox-talks/requirement-mappings/content-options");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        options!.Should().NotContain(o => o.Id == talkId);
    }

    [Fact]
    public async Task ContentOptions_PublishedActiveTalk_Returned()
    {
        var talkId = await CreateTalkAsync(nameof(ContentOptions_PublishedActiveTalk_Returned), ToolboxTalkStatus.Published, isActive: true);

        var (response, options) = await AdminClient.GetWithResponseAsync<List<ContentOptionDto>>(
            "/api/toolbox-talks/requirement-mappings/content-options");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        options!.Should().Contain(o => o.Id == talkId && o.Type == "Talk");
    }
}
