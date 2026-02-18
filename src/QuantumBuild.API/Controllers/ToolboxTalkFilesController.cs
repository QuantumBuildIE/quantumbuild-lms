using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.Common.Interfaces;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Storage;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for managing Toolbox Talk file uploads (videos, PDFs)
/// </summary>
[ApiController]
[Route("api/toolbox-talks/{toolboxTalkId:guid}")]
[Authorize(Policy = "Learnings.Manage")]
public class ToolboxTalkFilesController : ControllerBase
{
    private readonly IR2StorageService _storageService;
    private readonly IToolboxTalksDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ToolboxTalkFilesController> _logger;

    // Size limits
    private const long MaxVideoSizeBytes = 500 * 1024 * 1024; // 500MB
    private const long MaxPdfSizeBytes = 50 * 1024 * 1024;    // 50MB

    private static readonly string[] AllowedVideoTypes = ["video/mp4", "video/webm", "video/quicktime"];
    private static readonly string[] AllowedPdfTypes = ["application/pdf"];

    public ToolboxTalkFilesController(
        IR2StorageService storageService,
        IToolboxTalksDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<ToolboxTalkFilesController> logger)
    {
        _storageService = storageService;
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Upload a video for a Toolbox Talk
    /// </summary>
    /// <param name="toolboxTalkId">The Toolbox Talk ID</param>
    /// <param name="file">The video file to upload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result with public URL</returns>
    [HttpPost("video")]
    [RequestSizeLimit(524288000)] // 500MB
    [ProducesResponseType(typeof(VideoUploadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadVideo(
        Guid toolboxTalkId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        // Validate file presence
        if (file == null || file.Length == 0)
            return BadRequest(Result.Fail<VideoUploadResponseDto>("No file provided"));

        // Validate content type
        if (!AllowedVideoTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(Result.Fail<VideoUploadResponseDto>(
                $"Invalid video type '{file.ContentType}'. Allowed: {string.Join(", ", AllowedVideoTypes)}"));

        // Validate size
        if (file.Length > MaxVideoSizeBytes)
            return BadRequest(Result.Fail<VideoUploadResponseDto>(
                $"Video size ({file.Length / 1024 / 1024}MB) exceeds maximum ({MaxVideoSizeBytes / 1024 / 1024}MB)"));

        // Get toolbox talk
        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId
                && t.TenantId == _currentUser.TenantId
                && !t.IsDeleted, cancellationToken);

        if (talk == null)
            return NotFound(Result.Fail<VideoUploadResponseDto>("Learning not found"));

        // Upload to R2
        await using var stream = file.OpenReadStream();
        var result = await _storageService.UploadVideoAsync(
            _currentUser.TenantId,
            toolboxTalkId,
            talk.Title,
            stream,
            file.FileName,
            cancellationToken);

        if (!result.Success)
            return BadRequest(Result.Fail<VideoUploadResponseDto>(result.ErrorMessage!));

        // Update entity
        talk.VideoUrl = result.PublicUrl;
        talk.VideoSource = VideoSource.DirectUrl;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video uploaded for ToolboxTalk {Id}: {Url}", toolboxTalkId, result.PublicUrl);

        return Ok(new VideoUploadResponseDto(
            result.PublicUrl!,
            file.FileName,
            result.FileSizeBytes!.Value,
            "Upload"));
    }

    /// <summary>
    /// Upload a PDF for a Toolbox Talk
    /// </summary>
    /// <param name="toolboxTalkId">The Toolbox Talk ID</param>
    /// <param name="file">The PDF file to upload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result with public URL</returns>
    [HttpPost("pdf")]
    [RequestSizeLimit(52428800)] // 50MB
    [ProducesResponseType(typeof(PdfUploadResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadPdf(
        Guid toolboxTalkId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(Result.Fail<PdfUploadResponseDto>("No file provided"));

        if (!AllowedPdfTypes.Contains(file.ContentType.ToLower()))
            return BadRequest(Result.Fail<PdfUploadResponseDto>(
                "Invalid file type. Only PDF files are allowed."));

        if (file.Length > MaxPdfSizeBytes)
            return BadRequest(Result.Fail<PdfUploadResponseDto>(
                $"PDF size ({file.Length / 1024 / 1024}MB) exceeds maximum ({MaxPdfSizeBytes / 1024 / 1024}MB)"));

        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId
                && t.TenantId == _currentUser.TenantId
                && !t.IsDeleted, cancellationToken);

        if (talk == null)
            return NotFound(Result.Fail<PdfUploadResponseDto>("Learning not found"));

        await using var stream = file.OpenReadStream();
        var result = await _storageService.UploadPdfAsync(
            _currentUser.TenantId,
            toolboxTalkId,
            talk.Title,
            stream,
            file.FileName,
            cancellationToken);

        if (!result.Success)
            return BadRequest(Result.Fail<PdfUploadResponseDto>(result.ErrorMessage!));

        // Update entity
        talk.PdfUrl = result.PublicUrl;
        talk.PdfFileName = file.FileName;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PDF uploaded for ToolboxTalk {Id}: {Url}", toolboxTalkId, result.PublicUrl);

        return Ok(new PdfUploadResponseDto(
            result.PublicUrl!,
            file.FileName,
            result.FileSizeBytes!.Value));
    }

    /// <summary>
    /// Set an external video URL for a Toolbox Talk
    /// </summary>
    /// <param name="toolboxTalkId">The Toolbox Talk ID</param>
    /// <param name="request">The video URL request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated video URL information</returns>
    [HttpPut("video-url")]
    [ProducesResponseType(typeof(SetVideoUrlResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetVideoUrl(
        Guid toolboxTalkId,
        [FromBody] SetVideoUrlRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Url))
            return BadRequest(Result.Fail<SetVideoUrlResponseDto>("Video URL is required"));

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(Result.Fail<SetVideoUrlResponseDto>("Invalid URL format. Must be a valid HTTP or HTTPS URL."));

        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId
                && t.TenantId == _currentUser.TenantId
                && !t.IsDeleted, cancellationToken);

        if (talk == null)
            return NotFound(Result.Fail<SetVideoUrlResponseDto>("Learning not found"));

        // Update entity
        talk.VideoUrl = request.Url;
        talk.VideoSource = VideoSource.DirectUrl;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("External video URL set for ToolboxTalk {Id}: {Url}", toolboxTalkId, request.Url);

        return Ok(new SetVideoUrlResponseDto(request.Url, "ExternalUrl"));
    }

    /// <summary>
    /// Delete the video for a Toolbox Talk
    /// </summary>
    /// <param name="toolboxTalkId">The Toolbox Talk ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("video")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteVideo(
        Guid toolboxTalkId,
        CancellationToken cancellationToken)
    {
        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId
                && t.TenantId == _currentUser.TenantId
                && !t.IsDeleted, cancellationToken);

        if (talk == null)
            return NotFound(Result.Fail("Learning not found"));

        // Delete from R2 storage
        await _storageService.DeleteVideoAsync(
            _currentUser.TenantId,
            toolboxTalkId,
            cancellationToken);

        // Clear video fields in entity
        talk.VideoUrl = null;
        talk.VideoSource = VideoSource.None;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Video deleted for ToolboxTalk {Id}", toolboxTalkId);

        return NoContent();
    }

    /// <summary>
    /// Delete the PDF for a Toolbox Talk
    /// </summary>
    /// <param name="toolboxTalkId">The Toolbox Talk ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("pdf")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePdf(
        Guid toolboxTalkId,
        CancellationToken cancellationToken)
    {
        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId
                && t.TenantId == _currentUser.TenantId
                && !t.IsDeleted, cancellationToken);

        if (talk == null)
            return NotFound(Result.Fail("Learning not found"));

        // Delete from R2 storage
        await _storageService.DeletePdfAsync(
            _currentUser.TenantId,
            toolboxTalkId,
            cancellationToken);

        // Clear PDF fields in entity
        talk.PdfUrl = null;
        talk.PdfFileName = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PDF deleted for ToolboxTalk {Id}", toolboxTalkId);

        return NoContent();
    }

    /// <summary>
    /// Delete all files associated with a Toolbox Talk
    /// </summary>
    /// <param name="toolboxTalkId">The Toolbox Talk ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("files")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteFiles(
        Guid toolboxTalkId,
        CancellationToken cancellationToken)
    {
        var talk = await _dbContext.ToolboxTalks
            .FirstOrDefaultAsync(t => t.Id == toolboxTalkId
                && t.TenantId == _currentUser.TenantId
                && !t.IsDeleted, cancellationToken);

        if (talk == null)
            return NotFound(Result.Fail("Learning not found"));

        await _storageService.DeleteToolboxTalkFilesAsync(
            _currentUser.TenantId,
            toolboxTalkId,
            cancellationToken);

        // Clear URLs in entity
        talk.VideoUrl = null;
        talk.VideoSource = VideoSource.None;
        talk.PdfUrl = null;
        talk.PdfFileName = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("All files deleted for ToolboxTalk {Id}", toolboxTalkId);

        return NoContent();
    }
}
