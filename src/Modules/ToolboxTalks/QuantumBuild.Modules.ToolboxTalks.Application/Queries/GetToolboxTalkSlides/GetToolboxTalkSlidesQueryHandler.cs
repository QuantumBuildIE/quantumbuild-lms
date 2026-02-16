using MediatR;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;

namespace QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkSlides;

public class GetToolboxTalkSlidesQueryHandler
    : IRequestHandler<GetToolboxTalkSlidesQuery, List<SlideDto>>
{
    private readonly IToolboxTalksDbContext _context;
    private readonly IR2StorageService _r2StorageService;

    public GetToolboxTalkSlidesQueryHandler(
        IToolboxTalksDbContext context,
        IR2StorageService r2StorageService)
    {
        _context = context;
        _r2StorageService = r2StorageService;
    }

    public async Task<List<SlideDto>> Handle(
        GetToolboxTalkSlidesQuery request,
        CancellationToken cancellationToken)
    {
        var slides = await _context.ToolboxTalkSlides
            .Include(s => s.Translations)
            .Where(s => s.ToolboxTalkId == request.ToolboxTalkId
                && s.TenantId == request.TenantId
                && !s.IsDeleted)
            .OrderBy(s => s.PageNumber)
            .ToListAsync(cancellationToken);

        return slides.Select(slide =>
        {
            // Use translated text if language is specified and translation exists
            string? text = slide.OriginalText;
            if (!string.IsNullOrEmpty(request.LanguageCode) && request.LanguageCode != "en")
            {
                var translation = slide.Translations
                    .FirstOrDefault(t => t.LanguageCode == request.LanguageCode);
                if (translation != null)
                {
                    text = translation.TranslatedText;
                }
            }

            var imageUrl = _r2StorageService.GeneratePublicUrl(
                request.TenantId,
                "slides",
                $"{slide.ToolboxTalkId}/{slide.PageNumber}.png");

            return new SlideDto
            {
                Id = slide.Id,
                PageNumber = slide.PageNumber,
                ImageUrl = imageUrl,
                Text = text,
            };
        }).ToList();
    }
}
