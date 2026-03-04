namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Response for starting a translation validation run
/// </summary>
public record StartValidationResponse
{
    public Guid RunId { get; init; }
    public string JobId { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
