namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Result of a CSV bulk import for glossary terms
/// </summary>
public record ImportTermsResultDto
{
    public int Imported { get; init; }
    public int Skipped { get; init; }
    public List<string> Errors { get; init; } = [];
}
