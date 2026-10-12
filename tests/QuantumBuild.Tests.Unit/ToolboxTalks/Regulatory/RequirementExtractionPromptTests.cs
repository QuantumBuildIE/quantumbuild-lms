using FluentAssertions;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Regulatory;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Regulatory.StructureMaps;
using Xunit;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Regulatory;

/// <summary>
/// Characterises the per-standard extraction prompt and the tolerant response parser. The HIQA
/// snapshots pin the wording the prompt had before it became map-terminology-driven: any change to
/// the HIQA prompt fails here. Line endings are normalised on both sides because the prompt's
/// embedded newlines follow the source file's checkout line endings.
/// </summary>
public class RequirementExtractionPromptTests
{
    private static string Normalise(string s) => s.Replace("\r\n", "\n");

    private static StructureStandard HiqaStandardWithBothBlocks() => new("2.3", new[]
    {
        new StructureFeature("2.3.1", RequirementBlock.Person, "Person text."),
        new StructureFeature("2.3.2", RequirementBlock.Provider, "Provider text one."),
        new StructureFeature("2.3.3", RequirementBlock.Provider, "Provider text two."),
    });

    private static StructureStandard HiqaStandardWithNoPersonBlock() => new("12", new[]
    {
        new StructureFeature("12.1", RequirementBlock.Provider, "Provider text."),
    });

    private static StructureStandard RsaArticle6() => new("6", new[]
    {
        new StructureFeature("6(1)", RequirementBlock.Provider, "The daily driving time shall not exceed nine hours."),
        new StructureFeature("6(2)", RequirementBlock.Provider, "The weekly driving time shall not exceed 56 hours."),
    });

    private const string HiqaTail = """

        IMPORTANT RULES:
        - Copy text EXACTLY as printed in the document — no paraphrasing, no summarising, no correcting grammar or punctuation
        - Use the identifier strings exactly as listed above, unchanged (including any trailing punctuation)
        - Do NOT invent, split, merge, or omit any feature listed above
        - Do NOT include any feature that is not listed above, even if the document contains other numbered items nearby
        - Respond ONLY with the JSON array — no preamble, no markdown, no explanation

        DOCUMENT TEXT:
        DOC
        """;

    [Fact]
    public void Hiqa_BothBlocks_PromptIsUnchanged()
    {
        const string head = """
            You are transcribing verbatim text from a regulatory document. A structure map — authored and reviewed separately, NOT by you — has already determined exactly which features this document defines for Standard 2.3 (Principle 2). Your ONLY task is to locate each declared feature's exact printed text in the document below and copy it. Do not judge whether a feature belongs, do not paraphrase, summarise, reorder, merge, or omit any of them.

            The structure map declares these features for Standard 2.3:

            Person-experience features (numbered items describing what the person receiving the service experiences):
            - 2.3.1

            Provider features (numbered items describing what the service provider must have in place):
            - 2.3.2
            - 2.3.3

            For EACH of the 3 features listed above, find its exact verbatim text in the document text below (search near "Standard 2.3" and its person-experience/provider feature lists) and transcribe it exactly as printed. Exclude footnote marker glyphs and footnote text itself — footnotes are handled separately from this transcription.

            Respond ONLY with a valid JSON array of exactly 3 objects, one per feature listed above (any order), each shaped as:
            {"identifier": "<exact identifier exactly as listed above, unchanged>", "block": "Person" or "Provider", "verbatimText": "<the feature's exact printed text>"}
            """;

        var prompt = RequirementIngestionJob.BuildExtractionPrompt(
            "DOC", 2, HiqaStandardWithBothBlocks(), StructureTerminology.Hiqa);

        Normalise(prompt).Should().Be(Normalise(head + "\n" + HiqaTail));
    }

    [Fact]
    public void Hiqa_NoPersonFeatures_PromptIsUnchanged()
    {
        const string head = """
            You are transcribing verbatim text from a regulatory document. A structure map — authored and reviewed separately, NOT by you — has already determined exactly which features this document defines for Standard 12 (Principle 4). Your ONLY task is to locate each declared feature's exact printed text in the document below and copy it. Do not judge whether a feature belongs, do not paraphrase, summarise, reorder, merge, or omit any of them.

            The structure map declares these features for Standard 12:

            Person-experience features (numbered items describing what the person receiving the service experiences):


            Provider features (numbered items describing what the service provider must have in place):
            - 12.1

            For EACH of the 1 features listed above, find its exact verbatim text in the document text below (search near "Standard 12" and its person-experience/provider feature lists) and transcribe it exactly as printed. Exclude footnote marker glyphs and footnote text itself — footnotes are handled separately from this transcription.

            Respond ONLY with a valid JSON array of exactly 1 objects, one per feature listed above (any order), each shaped as:
            {"identifier": "<exact identifier exactly as listed above, unchanged>", "block": "Person" or "Provider", "verbatimText": "<the feature's exact printed text>"}
            """;

        var prompt = RequirementIngestionJob.BuildExtractionPrompt(
            "DOC", 4, HiqaStandardWithNoPersonBlock(), StructureTerminology.Hiqa);

        Normalise(prompt).Should().Be(Normalise(head + "\n" + HiqaTail));
    }

    [Fact]
    public void Rsa_Article6_UsesMapTerminology_AndNoHiqaWording()
    {
        var prompt = RequirementIngestionJob.BuildExtractionPrompt("DOC", 2, RsaArticle6(), RsaStructureMap.Terminology);

        prompt.Should().Contain("for Article 6 (Chapter 2)");
        prompt.Should().Contain("search near \"Article 6\"");
        prompt.Should().Contain("Provider features (obligations on drivers and transport undertakings set out in the Regulation):");
        prompt.Should().Contain("- 6(1)").And.Contain("- 6(2)");
        prompt.Should().Contain("\"block\": \"Provider\",");

        prompt.Should().NotContainEquivalentOf("Standard");
        prompt.Should().NotContainEquivalentOf("Principle");
        prompt.Should().NotContainEquivalentOf("person-experience");
        prompt.Should().NotContainEquivalentOf("service provider");
        prompt.Should().NotContain("Person");
    }

    private const string OneItem =
        "{\"identifier\": \"6(1)\", \"block\": \"Provider\", \"verbatimText\": \"Nine hours [see (a)].\"}";

    [Fact]
    public void Parse_ValidArray_IsParsed_WithoutStripping()
    {
        var result = RequirementIngestionJob.ExtractFeatures("[" + OneItem + "]", out var stripped);

        result.Should().ContainSingle().Which.Identifier.Should().Be("6(1)");
        stripped.Should().BeFalse();
    }

    [Fact]
    public void Parse_PreambleThenArray_IsParsed_AndFlaggedAsStripped()
    {
        var result = RequirementIngestionJob.ExtractFeatures(
            "Looking at the document, I found Article 6.\n\n[" + OneItem + "]", out var stripped);

        result.Should().ContainSingle().Which.VerbatimText.Should().Be("Nine hours [see (a)].");
        stripped.Should().BeTrue();
    }

    [Fact]
    public void Parse_ArrayThenTrailingProse_IsParsed()
    {
        var result = RequirementIngestionJob.ExtractFeatures(
            "[" + OneItem + "]\n\nNote: paragraph 4 repeats paragraph 1 [sic].", out var stripped);

        result.Should().ContainSingle();
        stripped.Should().BeTrue();
    }

    [Fact]
    public void Parse_FencedArrayWithTrailingProse_IsParsed()
    {
        var result = RequirementIngestionJob.ExtractFeatures(
            "```json\n[" + OneItem + "]\n```\nHope that helps.", out _);

        result.Should().ContainSingle();
    }

    [Fact]
    public void Parse_ProseOnly_IsInvalid()
    {
        RequirementIngestionJob.ExtractFeatures(
            "This regulation does not contain any \"Standard 12 (Principle 4)\".", out var stripped).Should().BeNull();
        stripped.Should().BeFalse();
    }

    [Fact]
    public void Parse_ProseWithUnbalancedBracket_IsInvalid()
    {
        RequirementIngestionJob.ExtractFeatures("I could not find [Article 8 anywhere", out _).Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyArray_StillValidAndEmpty()
    {
        var result = RequirementIngestionJob.ExtractFeatures("[]", out var stripped);

        result.Should().NotBeNull().And.BeEmpty();
        stripped.Should().BeFalse();
    }

    [Fact]
    public void Parse_BlankOrNullLiteral_IsInvalid()
    {
        RequirementIngestionJob.ExtractFeatures("   ", out _).Should().BeNull();
        RequirementIngestionJob.ExtractFeatures("null", out _).Should().BeNull();
    }
}
