namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Validation;

/// <summary>
/// Request body for editing a translated section and optionally its English source.
/// At least one of EditedTranslation or EditedOriginalText must be provided.
/// </summary>
public record EditTranslationRequest
{
    /// <summary>
    /// The edited translation text (existing behaviour). Required when not providing EditedOriginalText.
    /// </summary>
    public string? EditedTranslation { get; init; }

    /// <summary>
    /// Edited English source text. When set, the reviewer has corrected the original source.
    /// Staged on the ValidationResult row; propagated to the ToolboxTalk section on Accept.
    /// </summary>
    public string? EditedOriginalText { get; init; }

    /// <summary>
    /// When true and EditedOriginalText is provided, triggers a single-section re-validation
    /// using the new source text. When false, only persists the edit (draft save).
    /// </summary>
    public bool Revalidate { get; init; } = false;
}
