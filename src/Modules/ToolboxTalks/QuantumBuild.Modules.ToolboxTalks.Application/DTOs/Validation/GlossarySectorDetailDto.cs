namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Detailed DTO for a glossary sector with all its terms
/// </summary>
public record GlossarySectorDetailDto
{
    public Guid Id { get; init; }
    public string SectorKey { get; init; } = string.Empty;
    public string SectorName { get; init; } = string.Empty;
    public string? SectorIcon { get; init; }
    public bool IsSystemDefault { get; init; }
    public List<GlossaryTermDto> Terms { get; init; } = new();
}
