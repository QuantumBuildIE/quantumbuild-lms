namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// DTO for a glossary term
/// </summary>
public record GlossaryTermDto
{
    public Guid Id { get; init; }
    public string EnglishTerm { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsCritical { get; init; }
    public string Translations { get; init; } = "{}";
}
