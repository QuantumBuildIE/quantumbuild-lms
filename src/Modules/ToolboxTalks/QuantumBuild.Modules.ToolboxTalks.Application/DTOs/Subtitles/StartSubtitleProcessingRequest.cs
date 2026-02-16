using System.ComponentModel.DataAnnotations;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Subtitles;

/// <summary>
/// Request DTO for starting subtitle processing for a toolbox talk video
/// </summary>
public class StartSubtitleProcessingRequest
{
    /// <summary>
    /// URL of the video to process (can be a direct URL or platform-specific share link)
    /// </summary>
    [Required(ErrorMessage = "Video URL is required")]
    public string VideoUrl { get; set; } = string.Empty;

    /// <summary>
    /// Type of video source (GoogleDrive, AzureBlob, DirectUrl)
    /// </summary>
    [Required(ErrorMessage = "Video source type is required")]
    public SubtitleVideoSourceType VideoSourceType { get; set; }

    /// <summary>
    /// List of target languages to translate the subtitles to.
    /// English is always included as the source language.
    /// </summary>
    [Required(ErrorMessage = "At least one target language is required")]
    [MinLength(1, ErrorMessage = "At least one target language is required")]
    public List<string> TargetLanguages { get; set; } = new();
}
