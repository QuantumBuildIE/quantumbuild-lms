namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Request body for updating a glossary term
/// </summary>
public record UpdateTermRequest
{
    public string EnglishTerm { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsCritical { get; init; } = true;
    public string Translations { get; init; } = "{}";
}
