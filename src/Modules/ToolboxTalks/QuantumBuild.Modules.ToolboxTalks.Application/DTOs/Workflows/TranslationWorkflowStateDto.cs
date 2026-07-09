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
    public DateTime? LastExternalReviewedAt { get; init; }
    public string? LastExternalReviewedBy { get; init; }

    /// <summary>
    /// Per-section review provenance, one entry per section in <c>TranslatedSections</c> order.
    /// Empty when the translation doesn't exist yet or carries no sections. Consumers (badge,
    /// re-translate warning) derive coverage counts and full-vs-partial-scope from this list
    /// rather than from the two scalar fields above, which are a write-time aggregate only.
    /// </summary>
    public IReadOnlyList<SectionReviewStatusDto> SectionReviewStatuses { get; init; } = Array.Empty<SectionReviewStatusDto>();
}

/// <summary>
/// One section's external-review provenance, projected from <c>TranslatedSectionEntry</c>
/// for read-side consumers that don't need the full section content.
/// </summary>
public record SectionReviewStatusDto
{
    public int SectionIndex { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewedBy { get; init; }
}
