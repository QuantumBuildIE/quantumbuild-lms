namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Source of content for toolbox talk sections and questions
/// Indicates how the content was created
/// </summary>
public enum ContentSource
{
    /// <summary>
    /// Content was manually created by a user
    /// </summary>
    Manual = 1,

    /// <summary>
    /// Content was generated from video analysis/transcription
    /// </summary>
    Video = 2,

    /// <summary>
    /// Content was generated from PDF analysis
    /// </summary>
    Pdf = 3,

    /// <summary>
    /// Content was generated from both video and PDF sources
    /// </summary>
    Both = 4
}
