namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reviewers;

public record UpdateTenantReviewerConfigurationRequest
{
    public string ReviewerEmail { get; init; } = string.Empty;

    public string? ReviewerName { get; init; }
}
