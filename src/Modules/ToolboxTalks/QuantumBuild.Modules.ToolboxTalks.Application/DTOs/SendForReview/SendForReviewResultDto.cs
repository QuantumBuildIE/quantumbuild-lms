namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.SendForReview;

public record SendForReviewResultDto
{
    /// <summary>True only when not blocked and every language's invitation was initiated successfully.</summary>
    public bool Success { get; init; }

    /// <summary>True when the server-recomputed preview found a blocking issue; nothing was initiated.</summary>
    public bool Blocked { get; init; }

    public IReadOnlyList<BlockedLanguageDto> BlockedLanguages { get; init; } = Array.Empty<BlockedLanguageDto>();

    /// <summary>Per-language outcome of the InitiateExternalReview calls. Empty when Blocked is true.</summary>
    public IReadOnlyList<SendForReviewLanguageResultDto> LanguageResults { get; init; } = Array.Empty<SendForReviewLanguageResultDto>();
}

public record BlockedLanguageDto
{
    public string LanguageCode { get; init; } = string.Empty;
    public bool ReviewerMissing { get; init; }
    public bool WorkflowStateIneligible { get; init; }
}

public record SendForReviewLanguageResultDto
{
    public string LanguageCode { get; init; } = string.Empty;
    public bool Success { get; init; }
    public Guid? InvitationId { get; init; }
    public string? ErrorMessage { get; init; }
}
