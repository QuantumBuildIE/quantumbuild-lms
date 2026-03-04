namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Lightweight DTO for listing glossary sectors with term counts
/// </summary>
public record GlossarySectorListDto
{
    public Guid Id { get; init; }
    public string SectorKey { get; init; } = string.Empty;
    public string SectorName { get; init; } = string.Empty;
    public string? SectorIcon { get; init; }
    public bool IsSystemDefault { get; init; }
    public int TermCount { get; init; }
}
