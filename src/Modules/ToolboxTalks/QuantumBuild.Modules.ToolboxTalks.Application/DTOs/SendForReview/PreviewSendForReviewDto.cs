using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.SendForReview;

/// <summary>
/// How a language's reviewer was resolved against TenantReviewerConfiguration:
/// a row matching the language exactly, the tenant's null-language fallback row,
/// or no row at all.
/// </summary>
public enum ReviewerResolutionSource
{
    LanguageSpecific,
    Fallback,
    None
}

public record PreviewSendForReviewDto
{
    public Guid TalkId { get; init; }

    public IReadOnlyList<PreviewLanguageDto> Languages { get; init; } = Array.Empty<PreviewLanguageDto>();

    /// <summary>
    /// True if any listed language is missing a resolved reviewer or is not in an
    /// eligible workflow state. When true, SendForReviewCommand will refuse to initiate
    /// any invitations for this talk.
    /// </summary>
    public bool Blocked { get; init; }
}

public record PreviewLanguageDto
{
    public string LanguageCode { get; init; } = string.Empty;

    /// <summary>Sections with a Fail outcome in this language's most recent validation run, ordered by index.</summary>
    public IReadOnlyList<FailingSectionDto> FailingSections { get; init; } = Array.Empty<FailingSectionDto>();

    public int FailingSectionCount { get; init; }

    public string? ResolvedReviewerEmail { get; init; }

    public string? ResolvedReviewerName { get; init; }

    public ReviewerResolutionSource ResolutionSource { get; init; }

    /// <summary>
    /// True when this language's current TranslationWorkflowState permits InitiateExternalReview
    /// (Validated, ReviewerAccepted, or ThirdPartyReviewed).
    /// </summary>
    public bool WorkflowStateEligible { get; init; }

    /// <summary>This language's current TranslationWorkflowState, for surfacing state-specific guidance when ineligible.</summary>
    public TranslationWorkflowState CurrentWorkflowState { get; init; }
}

/// <summary>A section flagged as Fail, carrying the score that caused the flag so reviewers see why.</summary>
public record FailingSectionDto
{
    /// <summary>0-indexed section position in the validation run.</summary>
    public int Index { get; init; }

    /// <summary>TranslationValidationResult.FinalScore — the post-consensus score (0-100).</summary>
    public int Score { get; init; }

    public string? Title { get; init; }
}
