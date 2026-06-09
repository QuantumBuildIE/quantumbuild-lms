using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;

public record TranslationWorkflowStateDto
{
    public Guid TalkId { get; init; }
    public string LanguageCode { get; init; } = string.Empty;
    public TranslationWorkflowState State { get; init; }
    public string? LastEventType { get; init; }
    public DateTime? LastEventAt { get; init; }
    public string? TranslatedTitle { get; init; }
    public DateTime? TranslatedAt { get; init; }
    public bool NeedsRevalidation { get; init; }
    public ValidationOutcome? LastValidationOutcome { get; init; }
    public Guid? LastValidationRunId { get; init; }
    public int FlaggedWordCount { get; init; }
}
