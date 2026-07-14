using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

public record TranslationFlagDto
{
    public Guid Id { get; init; }
    public int StartOffset { get; init; }
    public int EndOffset { get; init; }
    public FlagSeverity Severity { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
