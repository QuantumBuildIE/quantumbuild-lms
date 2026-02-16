namespace QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

/// <summary>
/// Source type for videos being processed for subtitles
/// </summary>
public enum SubtitleVideoSourceType
{
    /// <summary>
    /// Video hosted on Google Drive
    /// </summary>
    GoogleDrive = 1,

    /// <summary>
    /// Video stored in Azure Blob Storage
    /// </summary>
    AzureBlob = 2,

    /// <summary>
    /// Direct URL to video file
    /// </summary>
    DirectUrl = 3
}
