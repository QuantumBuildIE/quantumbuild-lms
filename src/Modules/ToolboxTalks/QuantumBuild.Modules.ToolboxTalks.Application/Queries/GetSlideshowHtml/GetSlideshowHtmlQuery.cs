using MediatR;
using QuantumBuild.Core.Application.Models;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetSlideshowHtml;

public record GetSlideshowHtmlQuery : IRequest<Result<SlideshowHtmlDto>>
{
    public Guid TenantId { get; init; }
    public Guid ToolboxTalkId { get; init; }
    public string? LanguageCode { get; init; }
}

public record SlideshowHtmlDto
{
    public string Html { get; init; } = string.Empty;
    public string LanguageCode { get; init; } = string.Empty;
    public bool IsTranslated { get; init; }
    public DateTime GeneratedAt { get; init; }
}
