using System.Text.Json;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Translation;

namespace QuantumBuild.Tests.Unit.ToolboxTalks.Translation;

public class TranslatedSectionEntryTests
{
    [Fact]
    public void RoundTrip_WithProvenanceSet_PreservesReviewedAtAndReviewedBy()
    {
        var sectionId = Guid.NewGuid();
        var reviewedAt = new DateTime(2026, 7, 9, 10, 30, 0, DateTimeKind.Utc);
        var original = new List<TranslatedSectionEntry>
        {
            new()
            {
                SectionId = sectionId,
                Title = "Section Title",
                Content = "Section content",
                ReviewedAt = reviewedAt,
                ReviewedBy = "reviewer@example.com"
            }
        };

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<List<TranslatedSectionEntry>>(json);

        roundTripped.Should().HaveCount(1);
        roundTripped![0].SectionId.Should().Be(sectionId);
        roundTripped[0].Title.Should().Be("Section Title");
        roundTripped[0].Content.Should().Be("Section content");
        roundTripped[0].ReviewedAt.Should().Be(reviewedAt);
        roundTripped[0].ReviewedBy.Should().Be("reviewer@example.com");
    }

    [Fact]
    public void RoundTrip_WithProvenanceNull_PreservesNullsAndIncludesFieldsInJson()
    {
        var sectionId = Guid.NewGuid();
        var original = new List<TranslatedSectionEntry>
        {
            new() { SectionId = sectionId, Title = "Title", Content = "Content" }
        };

        var json = JsonSerializer.Serialize(original);

        // Codebase convention for this JSON blob: no JsonIgnoreCondition is configured anywhere
        // it's read/written, so System.Text.Json's default (include null-valued properties) applies.
        json.Should().Contain("\"ReviewedAt\":null");
        json.Should().Contain("\"ReviewedBy\":null");

        var roundTripped = JsonSerializer.Deserialize<List<TranslatedSectionEntry>>(json);

        roundTripped.Should().HaveCount(1);
        roundTripped![0].ReviewedAt.Should().BeNull();
        roundTripped[0].ReviewedBy.Should().BeNull();
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutProvenanceProperties_DefaultsToNull()
    {
        // Mirrors every TranslatedSections row persisted before Chunk A: only
        // SectionId/Title/Content were ever written.
        var sectionId = Guid.NewGuid();
        var legacyJson = $$"""
            [{"SectionId":"{{sectionId}}","Title":"Legacy Title","Content":"Legacy Content"}]
            """;

        var result = JsonSerializer.Deserialize<List<TranslatedSectionEntry>>(legacyJson);

        result.Should().HaveCount(1);
        result![0].SectionId.Should().Be(sectionId);
        result[0].Title.Should().Be("Legacy Title");
        result[0].Content.Should().Be("Legacy Content");
        result[0].ReviewedAt.Should().BeNull();
        result[0].ReviewedBy.Should().BeNull();
    }

    [Fact]
    public void WithExpression_MutatesOnlyTargetedField_PreservesProvenance()
    {
        // Exercises the exact pattern used by ContentCreationSessionService's
        // ExtractTranslatedSectionForId: `match with { SectionId = newSectionId }`.
        var original = new TranslatedSectionEntry
        {
            SectionId = Guid.NewGuid(),
            Title = "Title",
            Content = "Content",
            ReviewedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            ReviewedBy = "reviewer@example.com"
        };
        var newSectionId = Guid.NewGuid();

        var remapped = original with { SectionId = newSectionId };

        remapped.SectionId.Should().Be(newSectionId);
        remapped.Title.Should().Be(original.Title);
        remapped.Content.Should().Be(original.Content);
        remapped.ReviewedAt.Should().Be(original.ReviewedAt);
        remapped.ReviewedBy.Should().Be(original.ReviewedBy);
    }
}
