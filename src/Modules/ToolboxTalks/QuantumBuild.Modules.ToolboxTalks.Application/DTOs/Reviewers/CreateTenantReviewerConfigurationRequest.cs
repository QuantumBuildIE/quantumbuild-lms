namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reviewers;

public record CreateTenantReviewerConfigurationRequest
{
    /// <summary>
    /// Null means this is the tenant's "all languages" fallback reviewer.
    /// </summary>
    public string? LanguageCode { get; init; }

    public string ReviewerEmail { get; init; } = string.Empty;

    public string? ReviewerName { get; init; }
}
