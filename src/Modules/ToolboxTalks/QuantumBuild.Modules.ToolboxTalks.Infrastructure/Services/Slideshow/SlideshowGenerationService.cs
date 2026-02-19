using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;

namespace QuantumBuild.Modules.ToolboxTalks.Infrastructure.Services.Slideshow;

/// <summary>
/// Service that generates an AI-powered HTML slideshow from the PDF or video transcript
/// attached to a toolbox talk.
/// </summary>
public class SlideshowGenerationService : ISlideshowGenerationService
{
    private readonly IToolboxTalksDbContext _context;
    private readonly IAiSlideshowGenerationService _aiService;
    private readonly ITranscriptService _transcriptService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SlideshowGenerationService> _logger;

    public SlideshowGenerationService(
        IToolboxTalksDbContext context,
        IAiSlideshowGenerationService aiService,
        ITranscriptService transcriptService,
        HttpClient httpClient,
        ILogger<SlideshowGenerationService> logger)
    {
        _context = context;
        _aiService = aiService;
        _transcriptService = transcriptService;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateSlideshowAsync(
        Guid tenantId,
        Guid toolboxTalkId,
        string source = "pdf",
        CancellationToken cancellationToken = default)
    {
        var talk = await _context.ToolboxTalks
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId
                && t.TenantId == tenantId
                && !t.IsDeleted, cancellationToken);

        if (talk == null)
            return Result.Fail<string>("Learning not found");

        Result<string> result;

        if (string.Equals(source, "video", StringComparison.OrdinalIgnoreCase))
        {
            result = await GenerateFromVideoTranscriptAsync(talk, toolboxTalkId, cancellationToken);
        }
        else
        {
            result = await GenerateFromPdfAsync(talk, toolboxTalkId, cancellationToken);
        }

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
            "Successfully saved AI slideshow for talk {TalkId} from {Source}, HTML size: {Size} chars, SaveChanges wrote {Saved} rows",
            toolboxTalkId, source, result.Data.Length, saved);

        return Result.Ok(result.Data);
    }

    private async Task<Result<string>> GenerateFromPdfAsync(
        Domain.Entities.ToolboxTalk talk,
        Guid toolboxTalkId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(talk.PdfUrl))
            return Result.Fail<string>("No PDF attached to this talk");

        _logger.LogInformation(
            "Downloading PDF from {Url} for talk {TalkId}",
            talk.PdfUrl, toolboxTalkId);

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

        return await _aiService.GenerateSlideshowFromPdfAsync(
            pdfBytes, talk.Title, cancellationToken);
    }

    private async Task<Result<string>> GenerateFromVideoTranscriptAsync(
        Domain.Entities.ToolboxTalk talk,
        Guid toolboxTalkId,
        CancellationToken cancellationToken)
    {
        // First try ExtractedVideoTranscript on the entity
        var transcript = talk.ExtractedVideoTranscript;

        if (string.IsNullOrWhiteSpace(transcript))
        {
            // Fall back to SRT data via TranscriptService
            _logger.LogInformation(
                "No ExtractedVideoTranscript for talk {TalkId}, falling back to SRT transcript",
                toolboxTalkId);

            var transcriptResult = await _transcriptService.GetTranscriptAsync(
                toolboxTalkId, cancellationToken: cancellationToken);

            if (!transcriptResult.Success || string.IsNullOrWhiteSpace(transcriptResult.FullText))
            {
                return Result.Fail<string>(
                    "No video transcript available. Please run subtitle processing first to generate a transcript from the video.");
            }

            transcript = transcriptResult.FullText;
        }

        _logger.LogInformation(
            "Generating slideshow from video transcript for talk {TalkId}, transcript length: {Length} chars",
            toolboxTalkId, transcript.Length);

        return await _aiService.GenerateSlideshowFromTranscriptAsync(
            transcript, talk.Title, cancellationToken);
    }
}
