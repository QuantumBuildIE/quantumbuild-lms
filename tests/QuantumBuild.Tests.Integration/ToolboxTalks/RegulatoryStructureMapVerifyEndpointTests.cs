using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Tests.Integration.ToolboxTalks;

/// <summary>
/// POST /api/regulatory/structure-maps/{id}/verify — SuperUser only; 403 otherwise, 404 for an
/// unknown map, idempotent on an already-Verified map.
/// </summary>
[Collection("Integration")]
public class RegulatoryStructureMapVerifyEndpointTests : IntegrationTestBase
{
    public RegulatoryStructureMapVerifyEndpointTests(CustomWebApplicationFactory factory) : base(factory) { }

    private sealed record VerifyResponse(
        Guid Id, Guid RegulatoryDocumentId, string Status, string? VerifiedBy, DateTimeOffset? VerifiedAt);

    private async Task<Guid> CreateDraftMapAsync()
    {
        var context = GetDbContext();
        var body = new RegulatoryBody
        {
            Id = Guid.NewGuid(), Name = "Verify Test Body", Code = $"VT{Guid.NewGuid():N}"[..10], Country = "IE"
        };
        var doc = new RegulatoryDocument
        {
            Id = Guid.NewGuid(), RegulatoryBodyId = body.Id, Title = "Verify Test Document", Version = "1.0"
        };
        var map = new RegulatoryStructureMap
        {
            Id = Guid.NewGuid(),
            RegulatoryDocumentId = doc.Id,
            Status = RegulatoryStructureMapStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        context.RegulatoryBodies.Add(body);
        context.RegulatoryDocuments.Add(doc);
        context.RegulatoryStructureMaps.Add(map);
        await context.SaveChangesAsync();
        return map.Id;
    }

    [Fact]
    public async Task SuperUser_VerifiesDraftMap_RecordingVerifierAndTimestamp()
    {
        var mapId = await CreateDraftMapAsync();
        using var client = Factory.CreateSuperUserClient();
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        var response = await client.PostAsync($"/api/regulatory/structure-maps/{mapId}/verify", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<VerifyResponse>(
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        dto!.Status.Should().Be("Verified");
        dto.VerifiedBy.Should().NotBeNullOrWhiteSpace();
        dto.VerifiedAt.Should().BeAfter(before);

        var reloaded = await GetDbContext().RegulatoryStructureMaps.AsNoTracking().SingleAsync(m => m.Id == mapId);
        reloaded.Status.Should().Be(RegulatoryStructureMapStatus.Verified);
        reloaded.VerifiedBy.Should().Be(dto.VerifiedBy);
        reloaded.VerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task NonSuperUser_Gets403_AndMapStaysDraft()
    {
        var mapId = await CreateDraftMapAsync();

        var response = await AdminClient.PostAsync($"/api/regulatory/structure-maps/{mapId}/verify", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var reloaded = await GetDbContext().RegulatoryStructureMaps.AsNoTracking().SingleAsync(m => m.Id == mapId);
        reloaded.Status.Should().Be(RegulatoryStructureMapStatus.Draft);
        reloaded.VerifiedBy.Should().BeNull();
    }

    [Fact]
    public async Task Unauthenticated_Gets401()
    {
        var response = await UnauthenticatedClient.PostAsync($"/api/regulatory/structure-maps/{Guid.NewGuid()}/verify", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnknownMap_Gets404()
    {
        using var client = Factory.CreateSuperUserClient();

        var response = await client.PostAsync($"/api/regulatory/structure-maps/{Guid.NewGuid()}/verify", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RepeatVerify_IsIdempotent_AndKeepsOriginalStamp()
    {
        var mapId = await CreateDraftMapAsync();
        using var client = Factory.CreateSuperUserClient();

        var first = await client.PostAsync($"/api/regulatory/structure-maps/{mapId}/verify", null);
        var afterFirst = await GetDbContext().RegulatoryStructureMaps.AsNoTracking().SingleAsync(m => m.Id == mapId);
        await Task.Delay(50);
        var second = await client.PostAsync($"/api/regulatory/structure-maps/{mapId}/verify", null);
        var afterSecond = await GetDbContext().RegulatoryStructureMaps.AsNoTracking().SingleAsync(m => m.Id == mapId);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        afterSecond.Status.Should().Be(RegulatoryStructureMapStatus.Verified);
        afterSecond.VerifiedBy.Should().Be(afterFirst.VerifiedBy);
        afterSecond.VerifiedAt.Should().Be(afterFirst.VerifiedAt);
    }
}
