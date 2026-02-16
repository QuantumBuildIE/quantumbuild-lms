namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Status of a toolbox talk in the content creation workflow
/// </summary>
public enum ToolboxTalkStatus
{
    /// <summary>
    /// Initial state - content is being drafted
    /// </summary>
    Draft = 1,

    /// <summary>
    /// AI is processing video/PDF to generate content
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Content has been generated and is ready for human review
    /// </summary>
    ReadyForReview = 3,

    /// <summary>
    /// Content has been reviewed and published for use
    /// </summary>
    Published = 4
}
