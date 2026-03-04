namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Request body for editing a translated section
/// </summary>
public record EditTranslationRequest
{
    /// <summary>
    /// The edited translation text
    /// </summary>
    public string EditedTranslation { get; init; } = string.Empty;
}
