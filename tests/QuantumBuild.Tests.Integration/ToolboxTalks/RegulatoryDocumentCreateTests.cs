using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Infrastructure.Data;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// Covers the regulatory ingestion "create new regulation" sub-chunk: GET
/// /api/regulatory/bodies (body picker lookup) and POST /api/regulatory/documents (create).
/// New documents persist with LastIngestionStatus=Idle and no profiles — ingestion and
/// sector-profile setup remain separate, later actions triggered from the detail page.
///
/// RegulatoryDocument and RegulatoryBody are system-managed entities with no TenantId, so
/// there is no tenant-scoping dimension to test here.
/// </summary>
[Collection("Integration")]
public class RegulatoryDocumentCreateTests : IntegrationTestBase
{
    public RegulatoryDocumentCreateTests(CustomWebApplicationFactory factory) : base(factory) { }

    private static async Task<RegulatoryBody> CreateBodyAsync(ApplicationDbContext context, string? name = null)
    {
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(),
            Name = name ?? "Create Test Body",
            Code = $"CTB{Guid.NewGuid():N}"[..10],
            Country = "IE"
        };

        context.RegulatoryBodies.Add(body);
        await context.SaveChangesAsync();

        return body;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // GET /api/regulatory/bodies
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBodies_ReturnsAllRegulatoryBodies()
    {
        var context = GetDbContext();
        var bodyA = await CreateBodyAsync(context, "Health and Safety Authority");
        var bodyB = await CreateBodyAsync(context, "Food Safety Authority of Ireland");

        var (response, bodies) = await AdminClient.GetWithResponseAsync<List<RegulatoryBodyDto>>(
            "/api/regulatory/bodies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        bodies.Should().NotBeNull();
        bodies!.Select(b => b.Id).Should().Contain([bodyA.Id, bodyB.Id]);
    }

    [Fact]
    public async Task GetBodies_WithoutTenantManagePermission_Returns403()
    {
        await AssertForbiddenAsync(OperatorClient, "/api/regulatory/bodies");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // POST /api/regulatory/documents
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateDocument_ValidTitleVersionAndBody_Succeeds_PersistsIdleWithNoProfiles()
    {
        var context = GetDbContext();
        var body = await CreateBodyAsync(context);

        var request = new CreateRegulatoryDocumentRequest
        {
            RegulatoryBodyId = body.Id,
            Title = "New Home Support Standard",
            Version = "1.0"
        };

        var (response, dto) = await AdminClient.PostWithResponseAsync<CreateRegulatoryDocumentRequest, RegulatoryDocumentListDto>(
            "/api/regulatory/documents", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto.Should().NotBeNull();
        dto!.Title.Should().Be("New Home Support Standard");
        dto.Version.Should().Be("1.0");
        dto.RegulatoryBodyCode.Should().Be(body.Code);
        dto.SourceUrl.Should().BeNull();
        dto.SectorKeys.Should().BeEmpty();
        dto.DraftCount.Should().Be(0);
        dto.ApprovedCount.Should().Be(0);
        dto.RejectedCount.Should().Be(0);

        var freshContext = GetDbContext();
        var reloaded = await freshContext.RegulatoryDocuments.FirstAsync(d => d.Id == dto.Id);
        reloaded.LastIngestionStatus.Should().Be(RegulatoryIngestionStatus.Idle);
        reloaded.LastIngestedAt.Should().BeNull();
        reloaded.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateDocument_ValidHttpsSourceUrl_Succeeds()
    {
        var context = GetDbContext();
        var body = await CreateBodyAsync(context);

        var request = new CreateRegulatoryDocumentRequest
        {
            RegulatoryBodyId = body.Id,
            Title = "Document With Source",
            Version = "2.0",
            SourceUrl = "https://example.com/regulation.pdf"
        };

        var (response, dto) = await AdminClient.PostWithResponseAsync<CreateRegulatoryDocumentRequest, RegulatoryDocumentListDto>(
            "/api/regulatory/documents", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        dto!.SourceUrl.Should().Be("https://example.com/regulation.pdf");
    }

    [Fact]
    public async Task CreateDocument_InvalidSourceUrl_WindowsPath_Returns400()
    {
        var context = GetDbContext();
        var body = await CreateBodyAsync(context);

        var request = new CreateRegulatoryDocumentRequest
        {
            RegulatoryBodyId = body.Id,
            Title = "Document With Bad Source",
            Version = "1.0",
            SourceUrl = @"C:\Users\bob\document.pdf"
        };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryDocumentRequest, RegulatoryDocumentListDto>(
            "/api/regulatory/documents", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var freshContext = GetDbContext();
        var count = await freshContext.RegulatoryDocuments.CountAsync(d => d.RegulatoryBodyId == body.Id);
        count.Should().Be(0);
    }

    [Fact]
    public async Task CreateDocument_EmptyTitle_Returns400()
    {
        var context = GetDbContext();
        var body = await CreateBodyAsync(context);

        var request = new CreateRegulatoryDocumentRequest
        {
            RegulatoryBodyId = body.Id,
            Title = "",
            Version = "1.0"
        };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryDocumentRequest, RegulatoryDocumentListDto>(
            "/api/regulatory/documents", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDocument_EmptyVersion_Returns400()
    {
        var context = GetDbContext();
        var body = await CreateBodyAsync(context);

        var request = new CreateRegulatoryDocumentRequest
        {
            RegulatoryBodyId = body.Id,
            Title = "Document Missing Version",
            Version = ""
        };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryDocumentRequest, RegulatoryDocumentListDto>(
            "/api/regulatory/documents", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDocument_NonExistentRegulatoryBody_Returns400()
    {
        var request = new CreateRegulatoryDocumentRequest
        {
            RegulatoryBodyId = Guid.NewGuid(),
            Title = "Orphan Document",
            Version = "1.0"
        };

        var (response, _) = await AdminClient.PostWithResponseAsync<CreateRegulatoryDocumentRequest, RegulatoryDocumentListDto>(
            "/api/regulatory/documents", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateDocument_DuplicateTitle_BothPersist()
    {
        // RegulatoryDocument has no unique constraint on Title (only a non-unique index on
        // RegulatoryBodyId) — duplicates are a valid, if confusing, state. See
        // RegulatoryDocumentConfiguration.cs.
        var context = GetDbContext();
        var body = await CreateBodyAsync(context);

        var request = new CreateRegulatoryDocumentRequest
        {
            RegulatoryBodyId = body.Id,
            Title = "Duplicate Title Regulation",
            Version = "1.0"
        };

        var (firstResponse, firstDto) = await AdminClient.PostWithResponseAsync<CreateRegulatoryDocumentRequest, RegulatoryDocumentListDto>(
            "/api/regulatory/documents", request);
        var (secondResponse, secondDto) = await AdminClient.PostWithResponseAsync<CreateRegulatoryDocumentRequest, RegulatoryDocumentListDto>(
            "/api/regulatory/documents", request);

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        firstDto!.Id.Should().NotBe(secondDto!.Id);

        var freshContext = GetDbContext();
        var count = await freshContext.RegulatoryDocuments
            .CountAsync(d => d.Title == "Duplicate Title Regulation");
        count.Should().Be(2);
    }

    [Fact]
    public async Task CreateDocument_WithoutTenantManagePermission_Returns403()
    {
        var context = GetDbContext();
        var body = await CreateBodyAsync(context);

        var request = new CreateRegulatoryDocumentRequest
        {
            RegulatoryBodyId = body.Id,
            Title = "Forbidden Document",
            Version = "1.0"
        };

        var response = await OperatorClient.PostAsJsonAsync("/api/regulatory/documents", request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
