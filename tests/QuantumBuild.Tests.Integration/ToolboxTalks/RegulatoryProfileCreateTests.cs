using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Covers the regulatory profile creation sub-chunk: POST
/// /api/regulatory/documents/{documentId}/profiles — attaching a Sector to an existing
/// RegulatoryDocument. Per docs/regulatory-profile-creation-recon.md, profile creation is
/// inert setup: it never triggers ingestion and never touches other profiles/requirements.
///
/// RegulatoryDocument, RegulatoryBody, and RegulatoryProfile are system-managed entities with
/// no TenantId, so there is no tenant-scoping dimension to test here.
/// </summary>
[Collection("Integration")]
public class RegulatoryProfileCreateTests : IntegrationTestBase
{
    public RegulatoryProfileCreateTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static async Task<(RegulatoryBody Body, RegulatoryDocument Document)> CreateDocumentAsync(
        ApplicationDbContext context)
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];

        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = "Profile Test Body",
            Code = $"PTB{uniqueSuffix}"[..10],
            Country = "IE"
        };
        var document = new RegulatoryDocument
        {
            Id = Guid.NewGuid(),
            RegulatoryBodyId = body.Id,
            Title = "Profile Test Document",
            Version = "1.0"
        };

        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();

        return (body, document);
    }

    private static async Task<Sector> CreateSectorAsync(ApplicationDbContext context, string? key = null)
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Key = key ?? $"profile-test-{uniqueSuffix}",
            Name = $"Profile Test Sector {uniqueSuffix}",
            DisplayOrder = 99,
            IsActive = true
        };

        context.Sectors.Add(sector);
        await context.SaveChangesAsync();

        return sector;
    }

    [Fact]
    public async Task CreateProfile_ValidDocumentAndSector_Succeeds()
    {
        var context = GetDbContext();
        var (body, document) = await CreateDocumentAsync(context);
        var sector = await CreateSectorAsync(context);

        var request = new CreateRegulatoryProfileRequest { SectorId = sector.Id };

        var (response, dto) = await AdminClient.PostWithResponseAsync<CreateRegulatoryProfileRequest, RegulatoryProfileDto>(
            $"/api/regulatory/documents/{document.Id}/profiles", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.RegulatoryDocumentId.Should().Be(document.Id);
        dto.SectorId.Should().Be(sector.Id);
        dto.SectorKey.Should().Be(sector.Key);
        dto.SectorName.Should().Be(sector.Name);
        dto.ScoreLabel.Should().Be($"{body.Code} Regulatory Score");
        dto.ExportLabel.Should().Be($"{body.Code} Inspection Export");
        dto.IsActive.Should().BeTrue();

        var freshContext = GetDbContext();
        var persisted = await freshContext.RegulatoryProfiles.FirstAsync(p => p.Id == dto.Id);
        persisted.RegulatoryDocumentId.Should().Be(document.Id);
        persisted.SectorId.Should().Be(sector.Id);
        persisted.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProfile_DuplicateLivePair_Returns400_DoesNotCreateSecondRow()
    {
        var context = GetDbContext();
        var (_, document) = await CreateDocumentAsync(context);
        var sector = await CreateSectorAsync(context);

        var request = new CreateRegulatoryProfileRequest { SectorId = sector.Id };

        var (firstResponse, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryProfileRequest, RegulatoryProfileDto>(
            $"/api/regulatory/documents/{document.Id}/profiles", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var (secondResponse, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryProfileRequest, RegulatoryProfileDto>(
            $"/api/regulatory/documents/{document.Id}/profiles", request);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var freshContext = GetDbContext();
        var count = await freshContext.RegulatoryProfiles
            .CountAsync(p => p.RegulatoryDocumentId == document.Id && p.SectorId == sector.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task CreateProfile_SoftDeletedPair_RestoresExistingRow_DoesNotInsertDuplicate()
    {
        var context = GetDbContext();
        var (body, document) = await CreateDocumentAsync(context);
        var sector = await CreateSectorAsync(context);

        // ApplicationDbContext.SetAuditFields forces IsDeleted=false on Added entities, so a
        // soft-deleted row can't be seeded directly via Add — insert live, then Remove() to go
        // through the same soft-delete path the rest of the app uses.
        var softDeletedProfile = new RegulatoryProfile
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = document.Id,
            SectorId = sector.Id,
            SectorKey = sector.Key,
            ScoreLabel = "Stale Score",
            ExportLabel = "Stale Export",
            Description = "Pre-existing soft-deleted profile",
            IsActive = true
        };
        context.RegulatoryProfiles.Add(softDeletedProfile);
        await context.SaveChangesAsync();

        context.RegulatoryProfiles.Remove(softDeletedProfile);
        await context.SaveChangesAsync();

        var request = new CreateRegulatoryProfileRequest { SectorId = sector.Id };

        var (response, dto) = await AdminClient.PostWithResponseAsync<CreateRegulatoryProfileRequest, RegulatoryProfileDto>(
            $"/api/regulatory/documents/{document.Id}/profiles", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(softDeletedProfile.Id);

        var freshContext = GetDbContext();
        var count = await freshContext.RegulatoryProfiles
            .IgnoreQueryFilters()
            .CountAsync(p => p.RegulatoryDocumentId == document.Id && p.SectorId == sector.Id);
        count.Should().Be(1);

        var restored = await freshContext.RegulatoryProfiles.FirstAsync(p => p.Id == softDeletedProfile.Id);
        restored.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateProfile_NonExistentDocument_Returns400()
    {
        var context = GetDbContext();
        var sector = await CreateSectorAsync(context);

        var request = new CreateRegulatoryProfileRequest { SectorId = sector.Id };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryProfileRequest, RegulatoryProfileDto>(
            $"/api/regulatory/documents/{Guid.NewGuid()}/profiles", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateProfile_NonExistentSector_Returns400()
    {
        var context = GetDbContext();
        var (_, document) = await CreateDocumentAsync(context);

        var request = new CreateRegulatoryProfileRequest { SectorId = Guid.NewGuid() };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryProfileRequest, RegulatoryProfileDto>(
            $"/api/regulatory/documents/{document.Id}/profiles", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var freshContext = GetDbContext();
        var count = await freshContext.RegulatoryProfiles.CountAsync(p => p.RegulatoryDocumentId == document.Id);
        count.Should().Be(0);
    }

    [Fact]
    public async Task CreateProfile_DoesNotTriggerIngestion_StatusStaysIdle()
    {
        var context = GetDbContext();
        var (_, document) = await CreateDocumentAsync(context);
        var sector = await CreateSectorAsync(context);

        var request = new CreateRegulatoryProfileRequest { SectorId = sector.Id };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryProfileRequest, RegulatoryProfileDto>(
            $"/api/regulatory/documents/{document.Id}/profiles", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var freshContext = GetDbContext();
        var reloaded = await freshContext.RegulatoryDocuments.FirstAsync(d => d.Id == document.Id);
        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Idle);
        reloaded.LastIngestedAt.Should().BeNull();
    }

    [Fact]
    public async Task CreateProfile_WithoutTenantManagePermission_Returns403()
    {
        var context = GetDbContext();
        var (_, document) = await CreateDocumentAsync(context);
        var sector = await CreateSectorAsync(context);

        var request = new CreateRegulatoryProfileRequest { SectorId = sector.Id };

        var response = await OperatorClient.PostAsJsonAsync(
            $"/api/regulatory/documents/{document.Id}/profiles", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
