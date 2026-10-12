using System.Text.RegularExpressions;
using FluentAssertions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory.StructureMaps;
using Xunit;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Regulatory;

/// <summary>
/// Characterises RsaStructureMap.Principles — the seed content transcribed from the EUR-Lex
/// consolidated Regulation (EC) No 561/2006. Counts match the approved structure report.
/// </summary>
public class RsaStructureMapTests
{
    private static IEnumerable<StructureStandard> AllStandards =>
        RsaStructureMap.Principles.SelectMany(p => p.Standards);

    private static IEnumerable<StructureFeature> AllFeatures =>
        AllStandards.SelectMany(s => s.Features);

    private static StructureFeature Feature(string identifier) =>
        AllFeatures.Single(f => f.Identifier == identifier);

    [Fact]
    public void Source_DeclaresRsaBodyAndExactDocumentTitle()
    {
        RsaStructureMap.Source.RegulatoryBodyCode.Should().Be("RSA");
        RsaStructureMap.Source.DocumentTitle.Should().Be("Road Transport Regulations");
        RsaStructureMap.Source.Principles.Should().BeSameAs(RsaStructureMap.Principles);
    }

    [Fact]
    public void Principles_HasFourChaptersNineArticlesAndFortyProvisions()
    {
        RsaStructureMap.Principles.Select(p => p.Number).Should().Equal(2, 3, 4, 5);
        AllStandards.Should().HaveCount(9);
        AllFeatures.Should().HaveCount(40);
    }

    [Fact]
    public void Principles_ArticlesPerChapter_MatchApprovedReport()
    {
        RsaStructureMap.Principles.Select(p => string.Join(",", p.Standards.Select(s => s.Id)))
            .Should().Equal("5,6,7,8,9", "10", "12", "16,20");
    }

    [Fact]
    public void Principles_FeaturesPerArticle_MatchApprovedReport()
    {
        AllStandards.ToDictionary(s => s.Id, s => s.Features.Count).Should().Equal(new Dictionary<string, int>
        {
            ["5"] = 2, ["6"] = 5, ["7"] = 3, ["8"] = 11, ["9"] = 2,
            ["10"] = 5, ["12"] = 5, ["16"] = 4, ["20"] = 3,
        });
    }

    [Fact]
    public void Principles_FeaturesPerChapter_MatchApprovedReport()
    {
        RsaStructureMap.Principles.Select(p => p.Standards.Sum(s => s.Features.Count))
            .Should().Equal(23, 5, 5, 7);
    }

    [Fact]
    public void Features_AreAllProviderBlock()
    {
        AllFeatures.Should().OnlyContain(f => f.Block == RequirementBlock.Provider);
    }

    [Fact]
    public void Features_HaveUniqueIdentifiers_AndFitColumnLimits()
    {
        var features = AllFeatures.ToList();

        features.Select(f => f.Identifier).Should().OnlyHaveUniqueItems();
        features.Should().OnlyContain(f => f.Identifier.Length > 0 && f.Identifier.Length <= 20);
        features.Should().OnlyContain(f => f.VerbatimText.Length > 0 && f.VerbatimText.Length <= 2000);
        features.Where(f => f.FootnoteDefinition != null)
            .Should().OnlyContain(f => f.FootnoteDefinition!.Length <= 1000);
    }

    [Fact]
    public void Features_ContainNoConsolidationMarkersOrPageFooters()
    {
        var marker = new Regex(@"(^|\s)(M[1-4]|C1|B)(\s|$)|[►▼◄]|02006R0561", RegexOptions.Multiline);

        AllFeatures.Should().OnlyContain(f => !marker.IsMatch(f.VerbatimText));
    }

    [Fact]
    public void Features_ContainNoRejoinedLineWrapArtifacts()
    {
        var wrapSplits = new[] { "estab lishment", "compen sation", "under taking", "Regu lation" };

        AllFeatures.Should().OnlyContain(f => wrapSplits.All(s => !f.VerbatimText.Contains(s)));
    }

    [Fact]
    public void Features_ExcludedProvisions_AreAbsent()
    {
        var ids = AllFeatures.Select(f => f.Identifier).ToHashSet();

        ids.Should().NotContain(new[] { "8(2a)", "8(6a)", "8(10)", "9(1)", "7(para 3)", "10(5)(b)", "10(5)(c)", "16(5)" });
        AllStandards.Select(s => s.Id).Should().NotContain(new[] { "1", "2", "3", "4", "8a", "9a", "11", "13", "14", "15" });
    }

    // ── Spot checks: verbatim, markers stripped ──────────────────────────────────────────

    [Fact]
    public void Feature_6_1_IsVerbatim_WithSubparagraphOnOwnLine()
    {
        Feature("6(1)").VerbatimText.Should().Be(
            "The daily driving time shall not exceed nine hours.\n" +
            "However, the daily driving time may be extended to at most 10 hours not more than twice during the week.");
    }

    [Fact]
    public void Feature_6_5_SpansPageBreak_AndCarriesFootnote()
    {
        var f = Feature("6(5)");

        f.VerbatimText.Should().StartWith("A driver shall record as other work any time spent as described in point (e) of Article 4");
        f.VerbatimText.Should().Contain("of the Council (1). This record shall be entered either manually on a record sheet");
        f.VerbatimText.Should().EndWith("manual input facilities on recording equipment.");
        f.FootnoteDefinition.Should().StartWith("Regulation (EU) No 165/2014 of the European Parliament and of the Council of 4 February 2014");
        f.FootnoteDefinition.Should().EndWith("(OJ L 60, 28.2.2014, p. 1).");
    }

    [Fact]
    public void Feature_10_5_a_IsPointLevel_WithRejoinedRegulation_AndNoPointsBOrC()
    {
        var f = Feature("10(5)(a)");

        f.VerbatimText.Should().StartWith("(a) A transport undertaking which uses vehicles that are fitted with recording equipment complying with Annex IB of Regulation (EEC) No 3821/85");
        f.VerbatimText.Should().Contain("\n(i) ensure that all data are downloaded");
        f.VerbatimText.Should().Contain("\n(ii) ensure that all data downloaded from both the vehicle unit and driver card are kept for at least 12 months");
        f.VerbatimText.Should().EndWith("from the premises of the undertaking;");
        f.VerbatimText.Should().NotContain("`downloaded'");
    }

    [Fact]
    public void Feature_16_4_IsFirstTwoSubparagraphsOnly()
    {
        var f = Feature("16(4)");

        f.VerbatimText.Should().StartWith("For the purpose of roadside checks, until a digital journey form is available");
        f.VerbatimText.Should().EndWith("referred to in paragraph 5.");
        f.VerbatimText.Should().Contain("\n(b) carrying on board the vehicle paper or electronic copies");
        f.VerbatimText.Should().NotContain("simplify checks on compliance");
    }

    [Fact]
    public void Feature_8_8a_RejoinsWrappedWord()
    {
        Feature("8(8a)").VerbatimText.Should().Contain("taken in compensation for reduced weekly rest period.");
    }
}
