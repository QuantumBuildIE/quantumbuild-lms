namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Source platform for toolbox talk videos
/// </summary>
public enum VideoSource
{
    /// <summary>
    /// No video attached
    /// </summary>
    None = 0,

    /// <summary>
    /// YouTube video
    /// </summary>
    YouTube = 1,

    /// <summary>
    /// Google Drive hosted video
    /// </summary>
    GoogleDrive = 2,

    /// <summary>
    /// Vimeo video
    /// </summary>
    Vimeo = 3,

    /// <summary>
    /// Direct URL to video file
    /// </summary>
    DirectUrl = 4
}
