using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// B6 tests for the regulatory applicability surface (§1.2.2).
/// Covers the GET /api/regulatory/applicability endpoint and the
/// Applicability field added to RegulatoryScoreHistoryDto.
/// </summary>
[Collection("Integration")]
public class RegulatoryApplicabilityTests : IntegrationTestBase
{
    public RegulatoryApplicabilityTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helper — creates Sector + Body + Document + Profile in one shot
    // ─────────────────────────────────────────────────────────────────────────────

    private async Task<RegulatoryProfile> CreateProfileAsync(string sectorKey, string uniqueSuffix)
    {
        var context = GetDbContext();

        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Key = sectorKey,
            Name = $"B6 Test Sector {uniqueSuffix}",
            DisplayOrder = 99,
            IsActive = true
        };
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = $"B6 Test Body {uniqueSuffix}",
            Code = $"B6{uniqueSuffix}",
            Country = "IE"
        };
        var doc = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = $"B6 Test Document {uniqueSuffix}",
            Version = "1.0"
        };
        var profile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = doc.Id,
            SectorId = sector.Id,
            SectorKey = sectorKey,
            ScoreLabel = $"Test Score {uniqueSuffix}",
            ExportLabel = $"TSP{uniqueSuffix}",
            Description = "Integration test profile"
        };

        context.Sectors.Add(sector);
        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(doc);
        context.RegulatoryProfiles.Add(profile);
        await context.SaveChangesAsync();

        return profile;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // B6 Test 1 — GET /api/regulatory/applicability — unknown sector key
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetApplicability_UnknownSectorKey_ReturnsFalse()
    {
        var (response, dto) = await AdminClient.GetWithResponseAsync<RegulatoryApplicabilityDto>(
            "/api/regulatory/applicability?sectorKey=no-such-sector-zzz-b6");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.HasRegulatoryProfile.Should().BeFalse();
        dto.ApprovedRequirementCount.Should().Be(0);
        dto.ProfileName.Should().BeNull();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // B6 Test 2 — profile exists but all requirements are Draft → approved count = 0
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetApplicability_ProfileWithOnlyDraftRequirements_ReturnsZeroApproved()
    {
        const string sectorKey = "b6-draft-sector";
        var profile = await CreateProfileAsync(sectorKey, "T2");

        var context = GetDbContext();
        context.RegulatoryRequirements.AddRange(
            new RegulatoryRequirement
            {
                Id = Guid.NewGuid(),
                RegulatoryProfileId = profile.Id,
                Title = "Draft 1",
                IngestionStatus = RequirementIngestionStatus.Draft
            },
            new RegulatoryRequirement
            {
                Id = Guid.NewGuid(),
                RegulatoryProfileId = profile.Id,
                Title = "Draft 2",
                IngestionStatus = RequirementIngestionStatus.Draft
            }
        );
        await context.SaveChangesAsync();

        var (response, dto) = await AdminClient.GetWithResponseAsync<RegulatoryApplicabilityDto>(
            $"/api/regulatory/applicability?sectorKey={sectorKey}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.HasRegulatoryProfile.Should().BeTrue();
        dto.ApprovedRequirementCount.Should().Be(0);
        dto.ProfileName.Should().Be("Test Score T2");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // B6 Test 3 — GetScoreHistory populates Applicability with correct approved count
    //             3 Approved + 2 Draft → ApprovedRequirementCount == 3
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetScoreHistory_PopulatesApplicability_WhenRunHasSectorKey()
    {
        const string sectorKey = "b6-history-sector";
        var profile = await CreateProfileAsync(sectorKey, "T3");

        var context = GetDbContext();
        context.RegulatoryRequirements.AddRange(
            Enumerable.Range(1, 5).Select(i => new RegulatoryRequirement
            {
                Id = Guid.NewGuid(),
                RegulatoryProfileId = profile.Id,
                Title = $"Requirement {i}",
                IngestionStatus = i <= 3
                    ? RequirementIngestionStatus.Approved
                    : RequirementIngestionStatus.Draft
            })
        );

        var runId = Guid.NewGuid();
        context.TranslationValidationRuns.Add(new TranslationValidationRun
        {
            Id = runId,
            TenantId = TestTenantConstants.TenantId,
            LanguageCode = "pl",
            SectorKey = sectorKey,
            PassThreshold = 75,
            SourceLanguage = "en",
            Status = ValidationRunStatus.Completed,
            OverallOutcome = ValidationOutcome.Pass,
            OverallScore = 85
        });
        await context.SaveChangesAsync();

        var (response, dto) = await AdminClient.GetWithResponseAsync<RegulatoryScoreHistoryDto>(
            $"/api/toolbox-talks/validation-runs/{runId}/regulatory-score/history");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.ValidationRunId.Should().Be(runId);
        dto.Applicability.Should().NotBeNull();
        dto.Applicability!.HasRegulatoryProfile.Should().BeTrue();
        dto.Applicability.ApprovedRequirementCount.Should().Be(3);
    }
}
