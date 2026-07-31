using FluentAssertions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory;
using Xunit;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Regulatory;

/// <summary>
/// Direct unit tests of MapCoverageVerifier — the document-level feature-level completeness +
/// attribution check (docs/faithful-extraction-build-recon.md, sub-chunk 3). These call
/// MapCoverageVerifier.Verify directly against a small, fully-known, hand-built
/// DocumentStructureMap fixture (mirroring the fake-map fixture pattern
/// RegulatoryIngestionTests.CreateStructureMapAsync uses for the job-level integration tests),
/// rather than driving the whole RequirementIngestionJob pipeline — the "unexpected" and
/// "misattributed" failure classes are not reachable through the real Claude-response-driven
/// pipeline today (map-driven assembly structurally cannot invent either), so exercising them
/// requires calling the check directly with a deliberately-wrong candidate set. See
/// RegulatoryIngestionTests for the job-level regression coverage confirming the happy path still
/// succeeds with this check wired in.
/// </summary>
public class MapCoverageVerifierTests
{
    /// <summary>
    /// Two principles, two standards, four declared features. Standard 2.1's sole feature
    /// (2.1.1) is declared with no FootnoteDefinition — a legitimate map-side gap (mirrors the
    /// real HIQA 4.5.5 case) used by the known-gap test below.
    /// </summary>
    private static DocumentStructureMap BuildMap(RegulatoryStructureMapStatus status = RegulatoryStructureMapStatus.Verified)
    {
        var principle1 = new StructurePrinciple(1, new List<StructureStandard>
        {
            new StructureStandard("1.1", new List<StructureFeature>
            {
                new StructureFeature("1.1.1", RequirementBlock.Person, "Person feature 1.1.1 verbatim text.", "Footnote for 1.1.1."),
                new StructureFeature("1.1.2", RequirementBlock.Person, "Person feature 1.1.2 verbatim text."),
                new StructureFeature("1.1.1", RequirementBlock.Provider, "Provider feature 1.1.1 verbatim text."),
            }),
        });

        var principle2 = new StructurePrinciple(2, new List<StructureStandard>
        {
            new StructureStandard("2.1", new List<StructureFeature>
            {
                new StructureFeature("2.1.1", RequirementBlock.Person, "Person feature 2.1.1 verbatim text.", FootnoteDefinition: null),
            }),
        });

        return new DocumentStructureMap(
            Id: Guid.NewGuid(),
            RegulatoryDocumentId: Guid.NewGuid(),
            Status: status,
            VerifiedBy: status == RegulatoryStructureMapStatus.Verified ? "test-verifier" : null,
            VerifiedAt: status == RegulatoryStructureMapStatus.Verified ? DateTimeOffset.UtcNow : null,
            Principles: new List<StructurePrinciple> { principle1, principle2 });
    }

    /// <summary>Builds one correctly-attributed candidate per declared feature — the "extraction went perfectly" baseline tests mutate away from.</summary>
    private static List<CandidateFeature> FullyMatchingCandidates(DocumentStructureMap map) =>
        map.Principles
            .SelectMany(p => p.Standards.Select(s => (Principle: p, Standard: s)))
            .SelectMany(x => x.Standard.Features.Select(f =>
                new CandidateFeature(f.Identifier, f.Block, x.Standard.Id, x.Principle.Number)))
            .ToList();

    [Fact]
    public void Verify_FullCorrectlyAttributedCoverage_IsComplete()
    {
        var map = BuildMap();
        var candidates = FullyMatchingCandidates(map);

        var result = MapCoverageVerifier.Verify(map, candidates);

        result.IsComplete.Should().BeTrue();
        result.Missing.Should().BeEmpty();
        result.Unexpected.Should().BeEmpty();
        result.Misattributed.Should().BeEmpty();
    }

    [Fact]
    public void Verify_MissingDeclaredFeature_FailsAndNamesTheExactMissingIdentifier()
    {
        var map = BuildMap();
        var candidates = FullyMatchingCandidates(map)
            .Where(c => !(c.Identifier == "1.1.2" && c.Block == RequirementBlock.Person))
            .ToList();

        var result = MapCoverageVerifier.Verify(map, candidates);

        result.IsComplete.Should().BeFalse();
        result.Missing.Should().ContainSingle(f => f.Identifier == "1.1.2" && f.Block == RequirementBlock.Person);
        result.Unexpected.Should().BeEmpty();
        result.Misattributed.Should().BeEmpty();
    }

    [Fact]
    public void Verify_ExtraFeatureNotDeclaredByMap_FailsAndNamesTheInventedIdentifier()
    {
        var map = BuildMap();
        var candidates = FullyMatchingCandidates(map);
        candidates.Add(new CandidateFeature("9.9.9", RequirementBlock.Person, "1.1", 1));

        var result = MapCoverageVerifier.Verify(map, candidates);

        result.IsComplete.Should().BeFalse();
        result.Unexpected.Should().ContainSingle(f => f.Identifier == "9.9.9" && f.Block == RequirementBlock.Person);
        result.Missing.Should().BeEmpty();
        result.Misattributed.Should().BeEmpty();
    }

    [Fact]
    public void Verify_FeatureAttributedToWrongStandardAndPrinciple_FailsAsMisattributedNotMissingOrExtra()
    {
        var map = BuildMap();
        var candidates = FullyMatchingCandidates(map)
            .Select(c => c.Identifier == "2.1.1" && c.Block == RequirementBlock.Person
                // Map declares 2.1.1 under Standard 2.1 / Principle 2 — persist it under 1.1 / 1 instead.
                ? c with { StandardId = "1.1", PrincipleNumber = 1 }
                : c)
            .ToList();

        var result = MapCoverageVerifier.Verify(map, candidates);

        result.IsComplete.Should().BeFalse();
        result.Missing.Should().BeEmpty();
        result.Unexpected.Should().BeEmpty();
        result.Misattributed.Should().ContainSingle();

        var misattributed = result.Misattributed.Single();
        misattributed.Identifier.Should().Be("2.1.1");
        misattributed.Block.Should().Be(RequirementBlock.Person);
        misattributed.DeclaredStandardId.Should().Be("2.1");
        misattributed.DeclaredPrincipleNumber.Should().Be(2);
        misattributed.ActualStandardId.Should().Be("1.1");
        misattributed.ActualPrincipleNumber.Should().Be(1);
    }

    [Fact]
    public void Verify_MapDeclaredFeatureWithNullFootnote_DoesNotCauseAFalseCompletenessFailure()
    {
        // 2.1.1 is declared in BuildMap with FootnoteDefinition: null — a legitimate map-side gap
        // (mirrors the real HIQA 4.5.5 case). The check keys only on (Identifier, Block,
        // StandardId, PrincipleNumber) and never examines FootnoteDefinition (CandidateFeature
        // doesn't even carry the field) — a null footnote the map itself declares must not be
        // misread as an extraction miss.
        var map = BuildMap();
        map.AllFeatures.Single(f => f.Identifier == "2.1.1").FootnoteDefinition.Should().BeNull();

        var result = MapCoverageVerifier.Verify(map, FullyMatchingCandidates(map));

        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Verify_RunsIdenticallyForDraftAndVerifiedMaps()
    {
        // Verify(...) never receives the map's Status/IsVerified at all, so there is no branch
        // that could weaken or skip the check for an unverified (Draft) map — completeness is a
        // property of the map's declared content, independent of human verification state.
        var draftMap = BuildMap(RegulatoryStructureMapStatus.Draft);
        var verifiedMap = BuildMap(RegulatoryStructureMapStatus.Verified);

        var draftResult = MapCoverageVerifier.Verify(draftMap, FullyMatchingCandidates(draftMap));
        var verifiedResult = MapCoverageVerifier.Verify(verifiedMap, FullyMatchingCandidates(verifiedMap));

        draftResult.IsComplete.Should().BeTrue();
        verifiedResult.IsComplete.Should().BeTrue();
        draftResult.Missing.Should().BeEquivalentTo(verifiedResult.Missing);
        draftResult.Unexpected.Should().BeEquivalentTo(verifiedResult.Unexpected);
        draftResult.Misattributed.Should().BeEquivalentTo(verifiedResult.Misattributed);

        // And the same failure-detecting behaviour holds for a Draft map too — not just the
        // happy path.
        var draftWithGap = FullyMatchingCandidates(draftMap)
            .Where(c => !(c.Identifier == "1.1.2" && c.Block == RequirementBlock.Person))
            .ToList();
        var draftGapResult = MapCoverageVerifier.Verify(draftMap, draftWithGap);
        draftGapResult.IsComplete.Should().BeFalse();
        draftGapResult.Missing.Should().ContainSingle(f => f.Identifier == "1.1.2" && f.Block == RequirementBlock.Person);
    }
}
