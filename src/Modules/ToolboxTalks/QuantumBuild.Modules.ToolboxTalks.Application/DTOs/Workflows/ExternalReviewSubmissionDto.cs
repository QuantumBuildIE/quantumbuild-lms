using System.Text.Json.Serialization;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;

public record ExternalReviewEditedSectionDto
{
    [JsonPropertyName("sectionIndex")]
    public int SectionIndex { get; init; }

    [JsonPropertyName("translatedText")]
    public string TranslatedText { get; init; } = string.Empty;
}
