using MediatR;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkPreview;

/// <summary>
/// Query to retrieve a toolbox talk preview as an employee would see it,
/// with translated content applied for the specified language.
/// </summary>
public record GetToolboxTalkPreviewQuery : IRequest<ToolboxTalkPreviewDto?>
{
    public Guid TenantId { get; init; }
    public Guid ToolboxTalkId { get; init; }

    /// <summary>
    /// Language code for preview. If null or matches source language, returns original content.
    /// </summary>
    public string? LanguageCode { get; init; }
}
