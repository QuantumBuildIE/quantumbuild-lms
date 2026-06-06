namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Workflows;

public record InitiateExternalReviewResult
{
    public Guid InvitationId { get; init; }
    /// <summary>Raw token for constructing the invitation URL. Never stored — only its hash is persisted.</summary>
    public string Token { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
