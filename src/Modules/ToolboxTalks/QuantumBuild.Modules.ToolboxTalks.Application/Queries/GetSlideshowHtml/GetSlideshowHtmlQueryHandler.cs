using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetSlideshowHtml;

public class GetSlideshowHtmlQueryHandler : IRequestHandler<GetSlideshowHtmlQuery, Result<SlideshowHtmlDto>>
{
    private readonly IToolboxTalksDbContext _context;

    public GetSlideshowHtmlQueryHandler(IToolboxTalksDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SlideshowHtmlDto>> Handle(GetSlideshowHtmlQuery request, CancellationToken cancellationToken)
    {
        var talk = await _context.ToolboxTalks
            .Include(t => t.SlideshowTranslations)
            .Where(t => t.Id == request.ToolboxTalkId && t.TenantId == request.TenantId && !t.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (talk == null)
            return Result.Fail<SlideshowHtmlDto>("Learning not found");

        if (string.IsNullOrEmpty(talk.SlideshowHtml))
            return Result.Fail<SlideshowHtmlDto>("No slideshow available for this talk");

        // Check if translation requested and available
        if (!string.IsNullOrEmpty(request.LanguageCode) &&
            !string.Equals(request.LanguageCode, talk.SourceLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            var translation = talk.SlideshowTranslations
                .FirstOrDefault(t => t.LanguageCode == request.LanguageCode);

            if (translation != null)
            {
                return Result.Ok(new SlideshowHtmlDto
                {
                    Html = translation.TranslatedHtml,
                    LanguageCode = translation.LanguageCode,
                    IsTranslated = true,
                    GeneratedAt = translation.TranslatedAt
                });
            }
        }

        // Return source language version
        return Result.Ok(new SlideshowHtmlDto
        {
            Html = talk.SlideshowHtml,
            LanguageCode = talk.SourceLanguageCode,
            IsTranslated = false,
            GeneratedAt = talk.SlideshowGeneratedAt ?? DateTime.UtcNow
        });
    }
}
