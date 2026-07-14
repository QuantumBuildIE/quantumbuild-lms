namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;

public record ExternalReviewEditedSectionDto
{
    public int SectionIndex { get; init; }
    public string TranslatedText { get; init; } = string.Empty;
}
