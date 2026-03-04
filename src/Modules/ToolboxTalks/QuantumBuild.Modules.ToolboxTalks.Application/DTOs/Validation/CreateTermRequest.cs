namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Request body for adding a term to a glossary sector
/// </summary>
public record CreateTermRequest
{
    public string EnglishTerm { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsCritical { get; init; } = true;
    public string Translations { get; init; } = "{}";
}
