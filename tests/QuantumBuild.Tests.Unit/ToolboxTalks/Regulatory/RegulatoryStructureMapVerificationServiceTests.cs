using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Domain.Entities;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Regulatory;

/// <summary>
/// Tests the minimal draft/verified state transition (VerifyAsync) and the reset-on-edit
/// mechanism (EditFeatureAsync) that guarantees a Verified map can never retain its stamp once its
/// content changes — see IRegulatoryStructureMapVerificationService.
/// </summary>
public class RegulatoryStructureMapVerificationServiceTests
{
    private readonly Mock<IToolboxTalksDbContext> _dbContext = new();

    private readonly List<RegulatoryStructureMap> _maps = new();
    private readonly List<RegulatoryStructureMapPrinciple> _principles = new();
    private readonly List<RegulatoryStructureMapStandard> _standards = new();
    private readonly List<RegulatoryStructureMapFeature> _features = new();

    public RegulatoryStructureMapVerificationServiceTests()
    {
        _dbContext.Setup(x => x.RegulatoryStructureMaps).Returns(MockDbSetFactory.Create(_maps).Object);
        _dbContext.Setup(x => x.RegulatoryStructureMapPrinciples).Returns(MockDbSetFactory.Create(_principles).Object);
        _dbContext.Setup(x => x.RegulatoryStructureMapStandards).Returns(MockDbSetFactory.Create(_standards).Object);
        _dbContext.Setup(x => x.RegulatoryStructureMapFeatures).Returns(MockDbSetFactory.Create(_features).Object);
        _dbContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private RegulatoryStructureMapVerificationService CreateSut() => new(_dbContext.Object);

    private (RegulatoryStructureMap Map, RegulatoryStructureMapFeature Feature) AddMapWithFeature(
        RegulatoryStructureMapStatus status = RegulatoryStructureMapStatus.Draft)
    {
        var map = new RegulatoryStructureMap { Id = Guid.NewGuid(), RegulatoryDocumentId = Guid.NewGuid(), Status = status };
        if (status == RegulatoryStructureMapStatus.Verified)
        {
            map.VerifiedBy = "original.verifier";
            map.VerifiedAt = DateTimeOffset.UtcNow.AddDays(-1);
        }
        _maps.Add(map);

        var principle = new RegulatoryStructureMapPrinciple { Id = Guid.NewGuid(), RegulatoryStructureMapId = map.Id, Number = 1, DisplayOrder = 0 };
        _principles.Add(principle);

        var standard = new RegulatoryStructureMapStandard { Id = Guid.NewGuid(), RegulatoryStructureMapPrincipleId = principle.Id, StandardId = "1.1", DisplayOrder = 0 };
        _standards.Add(standard);

        var feature = new RegulatoryStructureMapFeature
        {
            Id = Guid.NewGuid(),
            RegulatoryStructureMapStandardId = standard.Id,
            Identifier = "1.1.1",
            Block = RequirementBlock.Person,
            VerbatimText = "Original text",
            DisplayOrder = 0
        };
        _features.Add(feature);

        return (map, feature);
    }

    [Fact]
    public async Task VerifyAsync_SetsStatusVerifiedAndRecordsWhoAndWhen()
    {
        var (map, _) = AddMapWithFeature();
        var sut = CreateSut();

        await sut.VerifyAsync(map.Id, "jane.reviewer");

        map.Status.Should().Be(RegulatoryStructureMapStatus.Verified);
        map.VerifiedBy.Should().Be("jane.reviewer");
        map.VerifiedAt.Should().NotBeNull();
        map.VerifiedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task VerifyAsync_UnknownMapId_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();

        var act = () => sut.VerifyAsync(Guid.NewGuid(), "jane.reviewer");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task EditFeatureAsync_OnVerifiedMap_ResetsMapToDraftAndClearsVerificationMetadata()
    {
        // This is the core "editing verified content resets to draft" mechanism.
        var (map, feature) = AddMapWithFeature(RegulatoryStructureMapStatus.Verified);
        map.Status.Should().Be(RegulatoryStructureMapStatus.Verified); // sanity check on the fixture

        var sut = CreateSut();
        await sut.EditFeatureAsync(feature.Id, "Corrected text", "Corrected footnote");

        feature.VerbatimText.Should().Be("Corrected text");
        feature.FootnoteDefinition.Should().Be("Corrected footnote");
        map.Status.Should().Be(RegulatoryStructureMapStatus.Draft);
        map.VerifiedBy.Should().BeNull();
        map.VerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task EditFeatureAsync_OnDraftMap_LeavesStatusAsDraft()
    {
        var (map, feature) = AddMapWithFeature(RegulatoryStructureMapStatus.Draft);

        var sut = CreateSut();
        await sut.EditFeatureAsync(feature.Id, "Updated text", null);

        feature.VerbatimText.Should().Be("Updated text");
        map.Status.Should().Be(RegulatoryStructureMapStatus.Draft);
        map.VerifiedBy.Should().BeNull();
        map.VerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task EditFeatureAsync_UnknownFeatureId_ThrowsInvalidOperationException()
    {
        var sut = CreateSut();

        var act = () => sut.EditFeatureAsync(Guid.NewGuid(), "text", null);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
