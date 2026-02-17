using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Slideshow;

/// <summary>
/// Service that generates an AI-powered HTML slideshow from the PDF attached to a toolbox talk.
/// Replaces the previous mechanical PDF-to-PNG approach with Claude AI analysis and HTML generation.
/// </summary>
public class SlideshowGenerationService : ISlideshowGenerationService
{
    private readonly IToolboxTalksDbContext _context;
    private readonly IAiSlideshowGenerationService _aiService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlideshowGenerationService> _logger;

    public SlideshowGenerationService(
        IToolboxTalksDbContext context,
        IAiSlideshowGenerationService aiService,
        HttpClient httpClient,
        ILogger<SlideshowGenerationService> logger)
    {
        _context = context;
        _aiService = aiService;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateSlideshowAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        CancellationToken cancellationToken = default)
    {
        var talk = await _context.ToolboxTalks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId
                && t.TenantId == tenantId
                && !t.IsDeleted, cancellationToken);

        if (talk == null)
            return Result.Fail<string>("Learning not found");

        if (string.IsNullOrEmpty(talk.PdfUrl))
            return Result.Fail<string>("No PDF attached to this talk");

        _logger.LogInformation(
            "Downloading PDF from {Url} for talk {TalkId}",
            talk.PdfUrl, toolboxTalkId);

        // Download PDF
        byte[] pdfBytes;
        try
        {
            using var response = await _httpClient.GetAsync(talk.PdfUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to download PDF. Status: {StatusCode}", response.StatusCode);
                return Result.Fail<string>($"Failed to download PDF. HTTP status: {response.StatusCode}");
            }
            pdfBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download PDF from {PdfUrl}", talk.PdfUrl);
            return Result.Fail<string>($"Failed to download PDF: {ex.Message}");
        }

        // Generate HTML slideshow using AI
        var result = await _aiService.GenerateSlideshowFromPdfAsync(
            pdfBytes, talk.Title, cancellationToken);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Data))
        {
            return Result.Fail<string>(string.Join("; ", result.Errors));
        }

        // Save to database
        talk.SlideshowHtml = result.Data;
        talk.SlideshowGeneratedAt = DateTime.UtcNow;
        talk.SlidesGenerated = true;

        // Clear any old slideshow translations (they need to be regenerated)
        var oldTranslations = await _context.ToolboxTalkSlideshowTranslations
            .Where(t => t.ToolboxTalkId == toolboxTalkId)
            .ToListAsync(cancellationToken);

        if (oldTranslations.Any())
        {
            foreach (var translation in oldTranslations)
            {
                _context.ToolboxTalkSlideshowTranslations.Remove(translation);
            }
            _logger.LogInformation(
                "Removed {Count} old slideshow translations for talk {TalkId}",
                oldTranslations.Count, toolboxTalkId);
        }

        var saved = await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Successfully saved AI slideshow for talk {TalkId}, HTML size: {Size} chars, SaveChanges wrote {Saved} rows",
            toolboxTalkId, result.Data.Length, saved);

        return Result.Ok(result.Data);
    }
}
