using FluentAssertions;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory.StructureMaps;
using Xunit;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Regulatory;

/// <summary>
/// Tests against HiqaStructureMap.Principles — the pure seed content (not the runtime source of
/// truth any more; see RegulatoryStructureMapProviderTests for the DB-backed runtime path, and
/// RegulatoryStructureMapSeedDataTests for the seed that copies this content into the DB).
/// </summary>
public class HiqaStructureMapTests
{
    private static IEnumerable<QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory.StructureStandard> AllStandards =>
        HiqaStructureMap.Principles.SelectMany(p => p.Standards);

    private static IEnumerable<QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory.StructureFeature> AllFeatures =>
        AllStandards.SelectMany(s => s.Features);

    [Fact]
    public void Principles_HasExactlyOneHundredAndFiftyOneFeatures()
    {
        AllFeatures.Should().HaveCount(151);
    }

    [Fact]
    public void Principles_HasEightyThreePersonAndSixtyEightProviderFeatures()
    {
        var features = AllFeatures.ToList();

        features.Count(f => f.Block == RequirementBlock.Person).Should().Be(83);
        features.Count(f => f.Block == RequirementBlock.Provider).Should().Be(68);
    }

    [Fact]
    public void Principles_HasFourPrinciplesAndSeventeenStandards()
    {
        HiqaStructureMap.Principles.Should().HaveCount(4);
        AllStandards.Should().HaveCount(17);
    }

    [Fact]
    public void Feature_1_1_7_IsPresentAndInPersonBlock()
    {
        var feature = AllFeatures.Single(f => f.Identifier == "1.1.7");

        feature.Block.Should().Be(RequirementBlock.Person);
        feature.VerbatimText.Should().Contain("privacy and dignity");
    }

    [Fact]
    public void Standard_2_3_ProviderBlock_HasNoItemNumberedOne()
    {
        var standard = AllStandards.Single(s => s.Id == "2.3");

        standard.ProviderFeatures.Select(f => f.Identifier).Should().NotContain("2.3.1");
        standard.ProviderFeatures.Select(f => f.Identifier).Should().Contain("2.3.2");
        standard.ProviderFeatures.Should().HaveCount(7);
        standard.PersonFeatures.Should().HaveCount(7);
    }

    [Fact]
    public void Standard_3_2_ProviderFeature_IsCorrectlyBlockedDespiteHeadingWordingVariance()
    {
        // Standard 3.2 prints its provider heading with "a service provider" (like Principles 1/4)
        // even though it sits inside Principle 3 alongside 3.1/3.3, which use the "no a" wording.
        // Block attribution must come from the map's own authored data, not heading text — this
        // spot-checks that 3.2.1 provider is still correctly attributed.
        var standard = AllStandards.Single(s => s.Id == "3.2");

        standard.ProviderFeatures.Should().HaveCount(2);
        standard.PersonFeatures.Should().HaveCount(3);
        standard.Features.Single(f => f.Identifier == "3.2.1" && f.Block == RequirementBlock.Provider)
            .VerbatimText.Should().Contain("integrated within and between home support services");
    }

    [Fact]
    public void Standard_4_5_UsesTrailingPeriodIdentifiersLiterally()
    {
        var standard = AllStandards.Single(s => s.Id == "4.5");

        standard.Features.Select(f => f.Identifier).Should().Contain("4.5.1.");
        standard.Features.Select(f => f.Identifier).Should().NotContain("4.5.1");
    }

    [Theory]
    [InlineData("1.1", 8, 3)]
    [InlineData("1.2", 5, 3)]
    [InlineData("1.3", 8, 2)]
    [InlineData("1.4", 5, 4)]
    [InlineData("2.1", 4, 3)]
    [InlineData("2.2", 8, 5)]
    [InlineData("2.3", 7, 7)]
    [InlineData("2.4", 4, 4)]
    [InlineData("2.5", 2, 2)]
    [InlineData("3.1", 5, 5)]
    [InlineData("3.2", 3, 2)]
    [InlineData("3.3", 5, 5)]
    [InlineData("4.1", 5, 8)]
    [InlineData("4.2", 2, 2)]
    [InlineData("4.3", 3, 2)]
    [InlineData("4.4", 6, 6)]
    [InlineData("4.5", 3, 5)]
    public void Standard_HasExpectedPersonAndProviderCounts(string standardId, int expectedPerson, int expectedProvider)
    {
        var standard = AllStandards.Single(s => s.Id == standardId);

        standard.PersonFeatures.Should().HaveCount(expectedPerson);
        standard.ProviderFeatures.Should().HaveCount(expectedProvider);
    }

    [Fact]
    public void FootnoteDefinition_IsAttachedToFeaturesThatReferenceADefinedTerm()
    {
        var byId = AllFeatures.ToList();

        byId.Single(f => f.Identifier == "1.1.4" && f.Block == RequirementBlock.Person)
            .FootnoteDefinition.Should().Contain("Equal Status Act");
        byId.Single(f => f.Identifier == "4.1.5" && f.Block == RequirementBlock.Person)
            .FootnoteDefinition.Should().Contain("Equal Status Act");
        byId.Single(f => f.Identifier == "1.3.6" && f.Block == RequirementBlock.Person)
            .FootnoteDefinition.Should().Contain("Decision supporter");
        byId.Single(f => f.Identifier == "2.3.5" && f.Block == RequirementBlock.Person)
            .FootnoteDefinition.Should().Contain("collection agent");
    }

    [Fact]
    public void AllFeatureIdentifiers_AreUniquePerBlockPerStandard()
    {
        foreach (var standard in AllStandards)
        {
            standard.PersonFeatures.Select(f => f.Identifier).Should().OnlyHaveUniqueItems();
            standard.ProviderFeatures.Select(f => f.Identifier).Should().OnlyHaveUniqueItems();
        }
    }

    [Fact]
    public void AllFeatures_HaveNonEmptyVerbatimText()
    {
        AllFeatures.Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.VerbatimText));
    }
}
