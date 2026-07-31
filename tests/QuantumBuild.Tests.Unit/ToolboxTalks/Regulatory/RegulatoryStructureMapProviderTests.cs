using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Regulatory;

/// <summary>
/// Tests the DB-backed dispatch: RegulatoryStructureMapProvider reads RegulatoryStructureMap +
/// its Principle/Standard/Feature children (via IToolboxTalksDbContext, mocked here) and projects
/// them into a DocumentStructureMap. Covers both loud states (no_structure_map, unverified-but-
/// present) that must never be silent.
/// </summary>
public class RegulatoryStructureMapProviderTests
{
    private readonly Mock<IToolboxTalksDbContext> _dbContext = new();

    private readonly List<RegulatoryDocument> _documents = new();
    private readonly List<RegulatoryStructureMap> _maps = new();
    private readonly List<RegulatoryStructureMapPrinciple> _principles = new();
    private readonly List<RegulatoryStructureMapStandard> _standards = new();
    private readonly List<RegulatoryStructureMapFeature> _features = new();

    public RegulatoryStructureMapProviderTests()
    {
        _dbContext.Setup(x => x.RegulatoryDocuments).Returns(MockDbSetFactory.Create(_documents).Object);
        _dbContext.Setup(x => x.RegulatoryStructureMaps).Returns(MockDbSetFactory.Create(_maps).Object);
        _dbContext.Setup(x => x.RegulatoryStructureMapPrinciples).Returns(MockDbSetFactory.Create(_principles).Object);
        _dbContext.Setup(x => x.RegulatoryStructureMapStandards).Returns(MockDbSetFactory.Create(_standards).Object);
        _dbContext.Setup(x => x.RegulatoryStructureMapFeatures).Returns(MockDbSetFactory.Create(_features).Object);
    }

    private RegulatoryStructureMapProvider CreateSut() => new(_dbContext.Object);

    private RegulatoryDocument AddDocument(string title = "Draft National Standards for Home Support Services")
    {
        var document = new RegulatoryDocument { Id = Guid.NewGuid(), Title = title, RegulatoryBodyId = Guid.NewGuid() };
        _documents.Add(document);
        return document;
    }

    private RegulatoryStructureMap AddMap(Guid documentId, RegulatoryStructureMapStatus status = RegulatoryStructureMapStatus.Draft)
    {
        var map = new RegulatoryStructureMap { Id = Guid.NewGuid(), RegulatoryDocumentId = documentId, Status = status };
        _maps.Add(map);
        return map;
    }

    private RegulatoryStructureMapPrinciple AddPrinciple(Guid mapId, int number, int displayOrder)
    {
        var principle = new RegulatoryStructureMapPrinciple
        {
            Id = Guid.NewGuid(),
            RegulatoryStructureMapId = mapId,
            Number = number,
            DisplayOrder = displayOrder
        };
        _principles.Add(principle);
        return principle;
    }

    private RegulatoryStructureMapStandard AddStandard(Guid principleId, string standardId, int displayOrder)
    {
        var standard = new RegulatoryStructureMapStandard
        {
            Id = Guid.NewGuid(),
            RegulatoryStructureMapPrincipleId = principleId,
            StandardId = standardId,
            DisplayOrder = displayOrder
        };
        _standards.Add(standard);
        return standard;
    }

    private RegulatoryStructureMapFeature AddFeature(
        Guid standardId, string identifier, RequirementBlock block, string text, int displayOrder, string? footnote = null)
    {
        var feature = new RegulatoryStructureMapFeature
        {
            Id = Guid.NewGuid(),
            RegulatoryStructureMapStandardId = standardId,
            Identifier = identifier,
            Block = block,
            VerbatimText = text,
            FootnoteDefinition = footnote,
            DisplayOrder = displayOrder
        };
        _features.Add(feature);
        return feature;
    }

    [Fact]
    public async Task TryGetStructureMapAsync_NoMapForDocument_ReturnsFalseAndNullMap()
    {
        var document = AddDocument();

        var (found, map) = await CreateSut().TryGetStructureMapAsync(document.Id);

        found.Should().BeFalse();
        map.Should().BeNull();
    }

    [Fact]
    public async Task GetStructureMapAsync_NoMapForDocument_ThrowsLoudlyWithNoStructureMapErrorCode()
    {
        var document = AddDocument("Some Other Standard");

        var act = () => CreateSut().GetStructureMapAsync(document.Id);

        var exception = await act.Should().ThrowAsync<RegulatoryStructureMapNotFoundException>();
        exception.Which.RegulatoryDocumentId.Should().Be(document.Id);
        RegulatoryStructureMapNotFoundException.ErrorCode.Should().Be("no_structure_map");
    }

    [Fact]
    public async Task GetStructureMapAsync_UnknownDocumentId_StillThrowsLoudly()
    {
        // No RegulatoryDocument row at all — the exception path must still work (title lookup
        // falls back rather than throwing its own exception).
        var act = () => CreateSut().GetStructureMapAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<RegulatoryStructureMapNotFoundException>();
    }

    [Fact]
    public async Task TryGetStructureMapAsync_DraftMap_ReturnsFoundTrueWithUnverifiedStatus()
    {
        // A draft map is present, not missing — TryGet must not treat "unverified" as "not found".
        var document = AddDocument();
        var map = AddMap(document.Id, RegulatoryStructureMapStatus.Draft);
        var principle = AddPrinciple(map.Id, 1, 0);
        var standard = AddStandard(principle.Id, "1.1", 0);
        AddFeature(standard.Id, "1.1.1", RequirementBlock.Person, "Some verbatim text", 0);

        var (found, result) = await CreateSut().TryGetStructureMapAsync(document.Id);

        found.Should().BeTrue();
        result!.IsVerified.Should().BeFalse();
        result.Status.Should().Be(RegulatoryStructureMapStatus.Draft);
    }

    [Fact]
    public async Task TryGetStructureMapAsync_VerifiedMap_SurfacesVerificationMetadata()
    {
        var document = AddDocument();
        var verifiedAt = DateTimeOffset.UtcNow;
        var map = AddMap(document.Id, RegulatoryStructureMapStatus.Verified);
        map.VerifiedBy = "jane.reviewer";
        map.VerifiedAt = verifiedAt;

        var (found, result) = await CreateSut().TryGetStructureMapAsync(document.Id);

        found.Should().BeTrue();
        result!.IsVerified.Should().BeTrue();
        result.VerifiedBy.Should().Be("jane.reviewer");
        result.VerifiedAt.Should().Be(verifiedAt);
    }

    [Fact]
    public async Task GetStructureMapAsync_AssemblesFullPrincipleStandardFeatureTree()
    {
        var document = AddDocument();
        var map = AddMap(document.Id);

        var p1 = AddPrinciple(map.Id, 1, 0);
        var s11 = AddStandard(p1.Id, "1.1", 0);
        AddFeature(s11.Id, "1.1.1", RequirementBlock.Person, "Person text", 0);
        AddFeature(s11.Id, "1.1.1", RequirementBlock.Provider, "Provider text", 1);

        var p2 = AddPrinciple(map.Id, 2, 1);
        var s21 = AddStandard(p2.Id, "2.1", 0);
        AddFeature(s21.Id, "2.1.1", RequirementBlock.Person, "Another person text", 0);

        var result = await CreateSut().GetStructureMapAsync(document.Id);

        result.Id.Should().Be(map.Id);
        result.RegulatoryDocumentId.Should().Be(document.Id);
        result.Principles.Should().HaveCount(2);
        result.AllStandards.Should().HaveCount(2);
        result.AllFeatures.Should().HaveCount(3);

        var standard11 = result.AllStandards.Single(s => s.Id == "1.1");
        standard11.PersonFeatures.Should().ContainSingle(f => f.Identifier == "1.1.1" && f.VerbatimText == "Person text");
        standard11.ProviderFeatures.Should().ContainSingle(f => f.Identifier == "1.1.1" && f.VerbatimText == "Provider text");
    }

    [Fact]
    public async Task GetStructureMapAsync_OrdersPrinciplesStandardsAndFeaturesByDisplayOrder()
    {
        var document = AddDocument();
        var map = AddMap(document.Id);

        // Insert out of order deliberately — assembly must respect DisplayOrder, not insertion order.
        var p2 = AddPrinciple(map.Id, 2, 1);
        var p1 = AddPrinciple(map.Id, 1, 0);

        var s2 = AddStandard(p1.Id, "1.2", 1);
        var s1 = AddStandard(p1.Id, "1.1", 0);

        AddFeature(s1.Id, "1.1.2", RequirementBlock.Person, "Second", 1);
        AddFeature(s1.Id, "1.1.1", RequirementBlock.Person, "First", 0);

        var result = await CreateSut().GetStructureMapAsync(document.Id);

        result.Principles.Select(p => p.Number).Should().ContainInOrder(1, 2);
        result.Principles.First().Standards.Select(s => s.Id).Should().ContainInOrder("1.1", "1.2");
        result.Principles.First().Standards.First().Features.Select(f => f.VerbatimText).Should().ContainInOrder("First", "Second");
    }

    [Fact]
    public async Task GetStructureMapAsync_CarriesFootnoteDefinitionThrough()
    {
        var document = AddDocument();
        var map = AddMap(document.Id);
        var principle = AddPrinciple(map.Id, 1, 0);
        var standard = AddStandard(principle.Id, "1.1", 0);
        AddFeature(standard.Id, "1.1.4", RequirementBlock.Person, "text", 0, footnote: "Equal Status Act footnote text");

        var result = await CreateSut().GetStructureMapAsync(document.Id);

        result.AllFeatures.Single().FootnoteDefinition.Should().Be("Equal Status Act footnote text");
    }
}
