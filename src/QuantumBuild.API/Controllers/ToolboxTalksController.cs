using Hangfire;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.CreateToolboxTalk;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.DeleteToolboxTalk;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.GenerateContentTranslations;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.SmartGenerateContent;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalk;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Reports;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetSlideshowHtml;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkById;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkDashboard;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkPreview;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkSlides;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalks;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkSettings;
using QuantumBuild.Modules.ToolboxTalks.Application.Services;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.Queries;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;
using QuantumBuild.Modules.ToolboxTalks.Infrastructure.Jobs;
using System.ComponentModel.DataAnnotations;
using FileHashType = QuantumBuild.Modules.ToolboxTalks.Application.Services.FileHashType;
using ISlideshowGenerationService = QuantumBuild.Modules.ToolboxTalks.Application.Services.ISlideshowGenerationService;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for managing toolbox talk templates
/// </summary>
[ApiController]
[Route("api/toolbox-talks")]
[Authorize(Policy = "Learnings.View")]
public class ToolboxTalksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly IToolboxTalkReportsService _reportsService;
    private readonly IToolboxTalkExportService _exportService;
    private readonly IContentExtractionService _contentExtractionService;
    private readonly IContentDeduplicationService _deduplicationService;
    private readonly IR2StorageService _r2StorageService;
    private readonly ISlideshowGenerationService _slideshowGenerationService;
    private readonly ILogger<ToolboxTalksController> _logger;

    public ToolboxTalksController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        IToolboxTalkReportsService reportsService,
        IToolboxTalkExportService exportService,
        IContentExtractionService contentExtractionService,
        IContentDeduplicationService deduplicationService,
        IR2StorageService r2StorageService,
        ISlideshowGenerationService slideshowGenerationService,
        ILogger<ToolboxTalksController> logger)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _reportsService = reportsService;
        _exportService = exportService;
        _contentExtractionService = contentExtractionService;
        _deduplicationService = deduplicationService;
        _r2StorageService = r2StorageService;
        _slideshowGenerationService = slideshowGenerationService;
        _logger = logger;
    }

    /// <summary>
    /// Get all toolbox talks with pagination and filtering
    /// </summary>
    /// <param name="searchTerm">Optional search term for title or description</param>
    /// <param name="frequency">Optional filter by frequency</param>
    /// <param name="isActive">Optional filter by active status</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of toolbox talks</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ToolboxTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? searchTerm = null,
        [FromQuery] ToolboxTalkFrequency? frequency = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = new GetToolboxTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                SearchTerm = searchTerm,
                Frequency = frequency,
                IsActive = isActive,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving learnings");
            return StatusCode(500, Result.Fail("Error retrieving learnings"));
        }
    }

    /// <summary>
    /// Get a toolbox talk by ID
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <returns>Toolbox talk details with sections and questions</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ToolboxTalkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var query = new GetToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            var result = await _mediator.Send(query);
            if (result == null)
            {
                return NotFound(new { message = "Learning not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { message = "Error retrieving learning" });
        }
    }

    /// <summary>
    /// Preview a toolbox talk as an employee would see it, with translated content.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="lang">Optional language code for translated preview</param>
    /// <returns>Preview data with translated sections and questions (no correct answers)</returns>
    [HttpGet("{id:guid}/preview")]
    [ProducesResponseType(typeof(ToolboxTalkPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPreview(Guid id, [FromQuery] string? lang = null)
    {
        try
        {
            var query = new GetToolboxTalkPreviewQuery
            {
                TenantId = _currentUserService.TenantId,
                ToolboxTalkId = id,
                LanguageCode = lang
            };

            var result = await _mediator.Send(query);
            if (result == null)
            {
                return NotFound(new { message = "Learning not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving toolbox talk preview {ToolboxTalkId}", id);
            return StatusCode(500, new { message = "Error retrieving learning preview" });
        }
    }

    /// <summary>
    /// Get slides for a toolbox talk preview with optional translated text.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="lang">Optional language code for translated slide text</param>
    /// <returns>List of slides with translated text</returns>
    [HttpGet("{id:guid}/preview/slides")]
    [ProducesResponseType(typeof(List<SlideDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreviewSlides(Guid id, [FromQuery] string? lang = null)
    {
        try
        {
            var query = new GetToolboxTalkSlidesQuery
            {
                TenantId = _currentUserService.TenantId,
                ToolboxTalkId = id,
                LanguageCode = lang
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving preview slides for toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { message = "Error retrieving preview slides" });
        }
    }

    /// <summary>
    /// Get slideshow HTML for a toolbox talk with optional translation.
    /// Used for admin preview of the AI-generated HTML slideshow.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="lang">Optional language code for translated slideshow</param>
    /// <returns>Slideshow HTML with translation metadata</returns>
    [HttpGet("{id:guid}/slideshow-html")]
    [ProducesResponseType(typeof(SlideshowHtmlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSlideshowHtml(Guid id, [FromQuery] string? lang = null)
    {
        try
        {
            var result = await _mediator.Send(new GetSlideshowHtmlQuery
            {
                TenantId = _currentUserService.TenantId,
                ToolboxTalkId = id,
                LanguageCode = lang
            });

            if (!result.Success)
                return BadRequest(new { error = string.Join("; ", result.Errors) });

            return Ok(result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving slideshow HTML for toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { message = "Error retrieving slideshow HTML" });
        }
    }

    /// <summary>
    /// Create a new toolbox talk
    /// </summary>
    /// <param name="command">Toolbox talk creation data</param>
    /// <returns>Created toolbox talk</returns>
    [HttpPost]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ToolboxTalkDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateToolboxTalkCommand command)
    {
        try
        {
            var commandWithTenant = command with { TenantId = _currentUserService.TenantId };
            var result = await _mediator.Send(commandWithTenant);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { message = ex.Message, errors = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating learning");
            return StatusCode(500, new { message = "Error creating learning" });
        }
    }

    /// <summary>
    /// Update an existing toolbox talk
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="command">Updated toolbox talk data</param>
    /// <returns>Updated toolbox talk</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(ToolboxTalkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateToolboxTalkCommand command)
    {
        try
        {
            if (id != command.Id)
            {
                return BadRequest(new { message = "ID mismatch" });
            }

            var commandWithTenant = command with { TenantId = _currentUserService.TenantId };
            var result = await _mediator.Send(commandWithTenant);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { message = ex.Message, errors = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { message = "Error updating learning" });
        }
    }

    /// <summary>
    /// Delete a toolbox talk (soft delete)
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var command = new DeleteToolboxTalkCommand
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            await _mediator.Send(command);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { message = "Error deleting learning" });
        }
    }

    /// <summary>
    /// Get toolbox talks dashboard with KPIs and statistics
    /// </summary>
    /// <returns>Dashboard data with completion rates and overdue counts</returns>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ToolboxTalkDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard()
    {
        try
        {
            var query = new GetToolboxTalkDashboardQuery
            {
                TenantId = _currentUserService.TenantId
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving learnings dashboard");
            return StatusCode(500, Result.Fail("Error retrieving learnings dashboard"));
        }
    }

    /// <summary>
    /// Get toolbox talk settings for the current tenant
    /// </summary>
    /// <returns>Toolbox talk settings</returns>
    [HttpGet("settings")]
    [Authorize(Policy = "Learnings.View")]
    [ProducesResponseType(typeof(ToolboxTalkSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings()
    {
        try
        {
            var query = new GetToolboxTalkSettingsQuery
            {
                TenantId = _currentUserService.TenantId
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving learning settings");
            return StatusCode(500, Result.Fail("Error retrieving learning settings"));
        }
    }

    /// <summary>
    /// Update toolbox talk settings for the current tenant
    /// </summary>
    /// <param name="dto">Updated settings</param>
    /// <returns>Updated settings</returns>
    [HttpPut("settings")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(ToolboxTalkSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateToolboxTalkSettingsDto dto)
    {
        try
        {
            // TODO: Implement UpdateToolboxTalkSettingsCommand when available
            // For now, return the current settings as a placeholder
            var query = new GetToolboxTalkSettingsQuery
            {
                TenantId = _currentUserService.TenantId
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating learning settings");
            return StatusCode(500, Result.Fail("Error updating learning settings"));
        }
    }

    #region Content Extraction

    /// <summary>
    /// Extracts content from video transcript and/or PDF document for AI generation preview.
    /// This endpoint is for testing and previewing what content will be used for AI generation.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="request">Content extraction options</param>
    /// <returns>Extraction result with combined content and metadata</returns>
    [HttpPost("{id:guid}/extract-content")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(ContentExtractionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExtractContent(
        Guid id,
        [FromBody] ExtractContentRequest request)
    {
        try
        {
            var result = await _contentExtractionService.ExtractContentAsync(
                id,
                request.IncludeVideo,
                request.IncludePdf,
                _currentUserService.TenantId);

            if (!result.Success && result.Errors.Any())
            {
                // If the toolbox talk wasn't found, return 404
                if (result.Errors.Any(e => e.Contains("not found")))
                {
                    return NotFound(new { message = result.Errors.First(), errors = result.Errors, warnings = result.Warnings });
                }

                return BadRequest(new { message = "Content extraction failed", errors = result.Errors, warnings = result.Warnings });
            }

            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting content for toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, Result.Fail("Error extracting content"));
        }
    }

    #endregion

    #region Content Deduplication

    /// <summary>
    /// Checks if a file has been previously processed in another toolbox talk.
    /// Returns information about the source toolbox talk if a duplicate is found.
    /// </summary>
    /// <param name="id">Toolbox talk ID to check against</param>
    /// <param name="request">File hash and type to check</param>
    /// <returns>Duplicate check result with source toolbox talk info if found</returns>
    [HttpPost("{id:guid}/check-duplicate")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(DuplicateCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckDuplicate(
        Guid id,
        [FromBody] CheckDuplicateRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Checking for duplicate content. ToolboxTalkId: {Id}, FileType: {FileType}",
                id, request.FileType);

            // Verify the toolbox talk exists
            var query = new GetToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            var toolboxTalk = await _mediator.Send(query);
            if (toolboxTalk == null)
            {
                return NotFound(new { error = "Learning not found" });
            }

            // Calculate hash from URL if not provided
            var fileHash = request.FileHash;
            if (string.IsNullOrEmpty(fileHash) && !string.IsNullOrEmpty(request.FileUrl))
            {
                _logger.LogDebug("Calculating hash from URL: {Url}", request.FileUrl);
                fileHash = await _deduplicationService.CalculateFileHashFromUrlAsync(request.FileUrl);
            }

            if (string.IsNullOrEmpty(fileHash))
            {
                return BadRequest(new { error = "Either fileHash or fileUrl must be provided" });
            }

            var fileType = request.FileType.Equals("PDF", StringComparison.OrdinalIgnoreCase)
                ? FileHashType.Pdf
                : FileHashType.Video;

            var result = await _deduplicationService.CheckForDuplicateAsync(
                _currentUserService.TenantId,
                fileHash,
                fileType,
                id);

            return Ok(new DuplicateCheckResponse
            {
                IsDuplicate = result.IsDuplicate,
                FileHash = fileHash,
                SourceToolboxTalk = result.SourceToolboxTalk != null
                    ? new SourceToolboxTalkResponse
                    {
                        Id = result.SourceToolboxTalk.Id,
                        Title = result.SourceToolboxTalk.Title,
                        ProcessedAt = result.SourceToolboxTalk.ProcessedAt,
                        SectionCount = result.SourceToolboxTalk.SectionCount,
                        QuestionCount = result.SourceToolboxTalk.QuestionCount,
                        HasSlideshow = result.SourceToolboxTalk.HasSlideshow,
                        TranslationLanguages = result.SourceToolboxTalk.TranslationLanguages
                    }
                    : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for duplicate content for toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { error = "Error checking for duplicate content" });
        }
    }

    /// <summary>
    /// Reuses content from a source toolbox talk (sections, questions, translations).
    /// Call this instead of /generate when user chooses to reuse existing content.
    /// </summary>
    /// <param name="id">Target toolbox talk ID to copy content into</param>
    /// <param name="request">Source toolbox talk information</param>
    /// <returns>Result of the reuse operation</returns>
    [HttpPost("{id:guid}/reuse-content")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(ContentReuseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReuseContent(
        Guid id,
        [FromBody] ReuseContentRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Reusing content. TargetId: {TargetId}, SourceId: {SourceId}",
                id, request.SourceToolboxTalkId);

            // Verify the target toolbox talk exists
            var query = new GetToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            var toolboxTalk = await _mediator.Send(query);
            if (toolboxTalk == null)
            {
                return NotFound(new { error = "Target Learning not found" });
            }

            var result = await _deduplicationService.ReuseContentAsync(
                id,
                request.SourceToolboxTalkId,
                _currentUserService.TenantId);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(new ContentReuseResponse
            {
                Success = true,
                SectionsCopied = result.SectionsCopied,
                QuestionsCopied = result.QuestionsCopied,
                TranslationsCopied = result.TranslationsCopied,
                Message = $"Successfully reused content: {result.SectionsCopied} sections, {result.QuestionsCopied} questions, {result.TranslationsCopied} translations"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reusing content for toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { error = "Error reusing content" });
        }
    }

    /// <summary>
    /// Updates the file hash for a toolbox talk after file upload.
    /// Should be called after uploading a PDF or setting a video URL.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="request">File hash information</param>
    /// <returns>Success status</returns>
    [HttpPost("{id:guid}/update-file-hash")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFileHash(
        Guid id,
        [FromBody] UpdateFileHashRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Updating file hash. ToolboxTalkId: {Id}, FileType: {FileType}",
                id, request.FileType);

            // Verify the toolbox talk exists
            var query = new GetToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            var toolboxTalk = await _mediator.Send(query);
            if (toolboxTalk == null)
            {
                return NotFound(new { error = "Learning not found" });
            }

            // Calculate hash from URL if not provided
            var fileHash = request.FileHash;
            if (string.IsNullOrEmpty(fileHash) && !string.IsNullOrEmpty(request.FileUrl))
            {
                _logger.LogDebug("Calculating hash from URL: {Url}", request.FileUrl);
                fileHash = await _deduplicationService.CalculateFileHashFromUrlAsync(request.FileUrl);
            }

            if (string.IsNullOrEmpty(fileHash))
            {
                return BadRequest(new { error = "Either fileHash or fileUrl must be provided" });
            }

            var fileType = request.FileType.Equals("PDF", StringComparison.OrdinalIgnoreCase)
                ? FileHashType.Pdf
                : FileHashType.Video;

            await _deduplicationService.UpdateFileHashAsync(
                id,
                fileHash,
                fileType,
                _currentUserService.TenantId);

            return Ok(new { success = true, fileHash });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating file hash for toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { error = "Error updating file hash" });
        }
    }

    #endregion

    #region Content Generation

    /// <summary>
    /// Starts AI content generation for a toolbox talk.
    /// This queues a background job that extracts content from video/PDF and generates
    /// sections and quiz questions using AI. Progress updates are sent via SignalR.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="request">Content generation options</param>
    /// <returns>Job information including the Hangfire job ID</returns>
    [HttpPost("{id:guid}/generate")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(GenerateContentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GenerateContentResponse>> GenerateContent(
        Guid id,
        [FromBody] GenerateContentRequest request)
    {
        try
        {
            // First, verify the toolbox talk exists by trying to get it
            var query = new GetToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            var toolboxTalk = await _mediator.Send(query);
            if (toolboxTalk == null)
            {
                return NotFound(new { error = "Learning not found" });
            }

            // Validate we have content to generate from
            if (request.IncludeVideo && string.IsNullOrEmpty(toolboxTalk.VideoUrl))
            {
                return BadRequest(new { error = "Video generation requested but no video URL is set" });
            }

            if (request.IncludePdf && string.IsNullOrEmpty(toolboxTalk.PdfUrl))
            {
                return BadRequest(new { error = "PDF generation requested but no PDF is uploaded" });
            }

            if (!request.IncludeVideo && !request.IncludePdf)
            {
                return BadRequest(new { error = "Must include at least one content source (video or PDF)" });
            }

            // Determine slideshow source
            var slideshowSource = request.SlideshowSource ?? (request.GenerateSlidesFromPdf == true ? "pdf" : "none");
            var generateSlides = !string.Equals(slideshowSource, "none", StringComparison.OrdinalIgnoreCase);

            // Create generation options
            var options = new ContentGenerationOptions(
                IncludeVideo: request.IncludeVideo,
                IncludePdf: request.IncludePdf,
                MinimumSections: request.MinimumSections ?? 7,
                MinimumQuestions: request.MinimumQuestions ?? 5,
                PassThreshold: request.PassThreshold ?? 80,
                ReplaceExisting: request.ReplaceExisting ?? true,
                SourceLanguageCode: request.SourceLanguageCode ?? "en",
                GenerateSlidesFromPdf: generateSlides,
                SlideshowSource: slideshowSource);

            // Queue the background job with tenant context
            var tenantId = _currentUserService.TenantId;
            var jobId = BackgroundJob.Enqueue<ContentGenerationJob>(
                job => job.ExecuteAsync(id, options, request.ConnectionId, tenantId, CancellationToken.None));

            _logger.LogInformation(
                "Queued content generation job {JobId} for toolbox talk {ToolboxTalkId}",
                jobId, id);

            return Accepted(new GenerateContentResponse(
                JobId: jobId,
                Message: "Content generation started. Connect to the SignalR hub for real-time progress updates.",
                ToolboxTalkId: id));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting content generation for toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { error = "Error starting content generation" });
        }
    }

    /// <summary>
    /// Manually triggers AI slideshow generation from PDF for a toolbox talk.
    /// Generates a self-contained HTML slideshow with animations.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="source">Content source: "pdf" (default) or "video" (uses transcript)</param>
    /// <returns>Generation result with HTML length</returns>
    [HttpPost("{id:guid}/generate-slides")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(GenerateSlidesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateSlides(Guid id, [FromQuery] string source = "pdf")
    {
        try
        {
            if (!string.Equals(source, "pdf", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(source, "video", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { error = "Invalid source parameter. Must be 'pdf' or 'video'." });
            }

            var query = new GetToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            var toolboxTalk = await _mediator.Send(query);
            if (toolboxTalk == null)
            {
                return NotFound(new { error = "Learning not found" });
            }

            if (string.Equals(source, "pdf", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrEmpty(toolboxTalk.PdfUrl))
            {
                return BadRequest(new { error = "No PDF is uploaded for this learning" });
            }

            var result = await _slideshowGenerationService.GenerateSlideshowAsync(
                _currentUserService.TenantId, id, source, HttpContext.RequestAborted);

            if (!result.Success)
            {
                return BadRequest(new { error = string.Join("; ", result.Errors) });
            }

            return Ok(new GenerateSlidesResponse
            {
                Success = true,
                Message = $"Slideshow generated successfully from {source}",
                HtmlLength = result.Data?.Length ?? 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating slides for toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { error = "Error generating slides" });
        }
    }

    /// <summary>
    /// Smart content generation: checks for duplicate sources, copies existing content,
    /// and generates only what's missing via AI. Returns immediately with reuse results
    /// and queues a background job if AI generation is needed.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="request">Smart generation options</param>
    /// <returns>Smart generation result showing what was copied vs what needs generation</returns>
    [HttpPost("{id:guid}/smart-generate")]
    [Authorize(Policy = "Learnings.Manage")]
    [ProducesResponseType(typeof(SmartGenerateContentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SmartGenerateContent(
        Guid id,
        [FromBody] SmartGenerateContentRequest request)
    {
        try
        {
            // First, verify the toolbox talk exists
            var query = new GetToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            var toolboxTalk = await _mediator.Send(query);
            if (toolboxTalk == null)
            {
                return NotFound(new { error = "Learning not found" });
            }

            // Validate we have content to work with
            var includeVideo = request.IncludeVideo && !string.IsNullOrEmpty(toolboxTalk.VideoUrl);
            var includePdf = request.IncludePdf && !string.IsNullOrEmpty(toolboxTalk.PdfUrl);

            if (!includeVideo && !includePdf)
            {
                return BadRequest(new { error = "Must include at least one available content source (video or PDF)" });
            }

            // Execute the smart generate command (synchronous: duplicate check + reuse)
            var command = new SmartGenerateContentCommand
            {
                TenantId = _currentUserService.TenantId,
                ToolboxTalkId = id,
                GenerateSections = request.GenerateSections,
                GenerateQuestions = request.GenerateQuestions,
                GenerateSlideshow = request.GenerateSlideshow,
                SourceLanguageCode = request.SourceLanguageCode ?? "en",
                MinimumSections = request.MinimumSections ?? 7,
                MinimumQuestions = request.MinimumQuestions ?? 5,
                PassThreshold = request.PassThreshold ?? 80,
                IncludeVideo = includeVideo,
                IncludePdf = includePdf,
                ConnectionId = request.ConnectionId,
            };

            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            string? jobId = null;
            string? missingTranslationsJobId = null;

            // If AI generation is needed, queue a Hangfire background job
            if (result.GenerationJobQueued)
            {
                var needSections = request.GenerateSections && result.SectionsCopied == 0;
                var needQuestions = request.GenerateQuestions && result.QuestionsCopied == 0;
                var needSlideshow = request.GenerateSlideshow && !result.SlideshowCopied;
                var slideshowSource = request.SlideshowSource ?? "none";

                var options = new ContentGenerationOptions(
                    IncludeVideo: includeVideo,
                    IncludePdf: includePdf,
                    MinimumSections: request.MinimumSections ?? 7,
                    MinimumQuestions: request.MinimumQuestions ?? 5,
                    PassThreshold: request.PassThreshold ?? 80,
                    ReplaceExisting: false, // Don't delete what we just copied!
                    SourceLanguageCode: request.SourceLanguageCode ?? "en",
                    GenerateSlidesFromPdf: needSlideshow,
                    SlideshowSource: needSlideshow ? slideshowSource : "none");

                var tenantId = _currentUserService.TenantId;
                jobId = BackgroundJob.Enqueue<ContentGenerationJob>(
                    job => job.ExecuteAsync(id, options, request.ConnectionId, tenantId, CancellationToken.None));

                result.GenerationJobId = jobId;

                _logger.LogInformation(
                    "Queued content generation job {JobId} for smart-generate on talk {ToolboxTalkId}. " +
                    "NeedSections: {NeedSections}, NeedQuestions: {NeedQuestions}, NeedSlideshow: {NeedSlideshow}",
                    jobId, id, needSections, needQuestions, needSlideshow);
            }
            else if (result.ContentCopiedFromTitle != null)
            {
                // Content was fully reused (no AI generation needed).
                // Queue a job to detect and generate any missing translations
                // (e.g., new languages added since the source content was created).
                var tenantId = _currentUserService.TenantId;
                missingTranslationsJobId = BackgroundJob.Enqueue<MissingTranslationsJob>(
                    job => job.ExecuteAsync(id, tenantId, request.ConnectionId, CancellationToken.None));

                _logger.LogInformation(
                    "Queued missing translations job {JobId} for talk {ToolboxTalkId} after full content reuse from '{SourceTitle}'",
                    missingTranslationsJobId, id, result.ContentCopiedFromTitle);
            }

            return Ok(new SmartGenerateContentResponse
            {
                SectionsCopied = result.SectionsCopied,
                QuestionsCopied = result.QuestionsCopied,
                SlideshowCopied = result.SlideshowCopied,
                TranslationsCopied = result.TranslationsCopied,
                SubtitlesCopied = result.SubtitlesCopied,
                SectionsGenerated = result.SectionsGenerated,
                QuestionsGenerated = result.QuestionsGenerated,
                SlideshowGenerated = result.SlideshowGenerated,
                ContentCopiedFromTitle = result.ContentCopiedFromTitle,
                GenerationJobId = jobId,
                GenerationJobQueued = result.GenerationJobQueued,
                MissingTranslationsJobId = missingTranslationsJobId,
                MissingTranslationsJobQueued = missingTranslationsJobId != null,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during smart content generation for toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { error = "Error during smart content generation" });
        }
    }

    #endregion

    #region Content Translations

    /// <summary>
    /// Generates content translations for a toolbox talk's sections and questions.
    /// Translates the title, description, sections, questions, and email templates.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="request">Languages to translate to</param>
    /// <returns>Translation results per language</returns>
    [HttpPost("{id:guid}/translations/generate")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(typeof(GenerateContentTranslationsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateTranslations(
        Guid id,
        [FromBody] GenerateTranslationsRequest request)
    {
        try
        {
            _logger.LogInformation(
                "Starting translation generation for ToolboxTalk {ToolboxTalkId}, Languages: {Languages}",
                id, string.Join(", ", request.Languages ?? new List<string>()));

            if (request.Languages == null || request.Languages.Count == 0)
            {
                return BadRequest(new { error = "At least one language is required" });
            }

            var command = new GenerateContentTranslationsCommand
            {
                ToolboxTalkId = id,
                TenantId = _currentUserService.TenantId,
                TargetLanguages = request.Languages
            };

            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Translation generation failed for ToolboxTalk {ToolboxTalkId}: {ErrorMessage}",
                    id, result.ErrorMessage);

                if (result.ErrorMessage?.Contains("not found") == true)
                {
                    return NotFound(new { error = result.ErrorMessage });
                }
                return BadRequest(new { error = result.ErrorMessage });
            }

            _logger.LogInformation(
                "Translation generation completed for ToolboxTalk {ToolboxTalkId}. Results: {ResultCount}",
                id, result.LanguageResults?.Count ?? 0);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating translations for toolbox talk {ToolboxTalkId}. Exception: {ExceptionType}, Message: {Message}",
                id, ex.GetType().Name, ex.Message);

            // Include more details in the error response to help diagnose issues
            return StatusCode(500, new {
                error = "Error generating translations",
                details = ex.Message,
                type = ex.GetType().Name
            });
        }
    }

    /// <summary>
    /// Gets existing content translations for a toolbox talk.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <returns>List of existing translations</returns>
    [HttpGet("{id:guid}/translations")]
    [ProducesResponseType(typeof(List<ContentTranslationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTranslations(Guid id)
    {
        try
        {
            var query = new GetToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            var toolboxTalk = await _mediator.Send(query);
            if (toolboxTalk == null)
            {
                return NotFound(new { error = "Learning not found" });
            }

            // Map translations from the toolbox talk
            var translations = toolboxTalk.Translations?.Select(t => new ContentTranslationDto
            {
                LanguageCode = t.LanguageCode,
                Language = t.Language,
                TranslatedTitle = t.TranslatedTitle,
                TranslatedAt = t.TranslatedAt,
                TranslationProvider = t.TranslationProvider
            }).ToList() ?? new List<ContentTranslationDto>();

            return Ok(translations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving translations for toolbox talk {ToolboxTalkId}", id);
            return StatusCode(500, new { error = "Error retrieving translations" });
        }
    }

    /// <summary>
    /// Deletes a specific content translation for a toolbox talk.
    /// </summary>
    /// <param name="id">Toolbox talk ID</param>
    /// <param name="languageCode">Language code to delete (e.g., "pl", "ro")</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}/translations/{languageCode}")]
    [Authorize(Policy = "Learnings.Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTranslation(Guid id, string languageCode)
    {
        try
        {
            // This would need a command, but for simplicity we can handle directly
            // In a production app, this should use a proper command pattern
            return StatusCode(501, new { error = "Delete translation not yet implemented" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting translation {LanguageCode} for toolbox talk {ToolboxTalkId}", languageCode, id);
            return StatusCode(500, new { error = "Error deleting translation" });
        }
    }

    #endregion

    #region Reports

    /// <summary>
    /// Get compliance report with breakdowns by department and talk
    /// </summary>
    /// <param name="dateFrom">Optional start date filter</param>
    /// <param name="dateTo">Optional end date filter</param>
    /// <param name="siteId">Optional site/department filter</param>
    /// <returns>Compliance report with metrics and breakdowns</returns>
    [HttpGet("reports/compliance")]
    [ProducesResponseType(typeof(ComplianceReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComplianceReport(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] Guid? siteId = null)
    {
        try
        {
            var report = await _reportsService.GetComplianceReportAsync(
                _currentUserService.TenantId,
                dateFrom,
                dateTo,
                siteId);
            return Ok(Result.Ok(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance report");
            return StatusCode(500, Result.Fail("Error generating compliance report"));
        }
    }

    /// <summary>
    /// Get list of overdue toolbox talk assignments
    /// </summary>
    /// <param name="siteId">Optional site/department filter</param>
    /// <param name="toolboxTalkId">Optional toolbox talk filter</param>
    /// <returns>List of overdue items</returns>
    [HttpGet("reports/overdue")]
    [ProducesResponseType(typeof(List<OverdueItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdueReport(
        [FromQuery] Guid? siteId = null,
        [FromQuery] Guid? toolboxTalkId = null)
    {
        try
        {
            var report = await _reportsService.GetOverdueReportAsync(
                _currentUserService.TenantId,
                siteId,
                toolboxTalkId);
            return Ok(Result.Ok(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating overdue report");
            return StatusCode(500, Result.Fail("Error generating overdue report"));
        }
    }

    /// <summary>
    /// Get detailed completion records with pagination
    /// </summary>
    /// <param name="dateFrom">Optional start date filter</param>
    /// <param name="dateTo">Optional end date filter</param>
    /// <param name="toolboxTalkId">Optional toolbox talk filter</param>
    /// <param name="siteId">Optional site/department filter</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>Paginated list of completion details</returns>
    [HttpGet("reports/completions")]
    [ProducesResponseType(typeof(PaginatedList<CompletionDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompletionsReport(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] Guid? toolboxTalkId = null,
        [FromQuery] Guid? siteId = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var report = await _reportsService.GetCompletionReportAsync(
                _currentUserService.TenantId,
                dateFrom,
                dateTo,
                toolboxTalkId,
                siteId,
                pageNumber,
                pageSize);
            return Ok(Result.Ok(report));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating completions report");
            return StatusCode(500, Result.Fail("Error generating completions report"));
        }
    }

    /// <summary>
    /// Export overdue report as Excel file
    /// </summary>
    /// <param name="siteId">Optional site/department filter</param>
    /// <param name="toolboxTalkId">Optional toolbox talk filter</param>
    /// <returns>Excel file</returns>
    [HttpGet("reports/overdue/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportOverdueReport(
        [FromQuery] Guid? siteId = null,
        [FromQuery] Guid? toolboxTalkId = null)
    {
        try
        {
            var report = await _reportsService.GetOverdueReportAsync(
                _currentUserService.TenantId,
                siteId,
                toolboxTalkId);

            var fileBytes = await _exportService.GenerateOverdueReportExcelAsync(report);

            if (fileBytes.Length == 0)
            {
                return BadRequest(Result.Fail("Export functionality is not yet implemented. Coming in Phase 2."));
            }

            var fileName = $"OverdueToolboxTalks_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting overdue report");
            return StatusCode(500, Result.Fail("Error exporting overdue report"));
        }
    }

    /// <summary>
    /// Export completions report as Excel file
    /// </summary>
    /// <param name="dateFrom">Optional start date filter</param>
    /// <param name="dateTo">Optional end date filter</param>
    /// <param name="toolboxTalkId">Optional toolbox talk filter</param>
    /// <param name="siteId">Optional site/department filter</param>
    /// <returns>Excel file</returns>
    [HttpGet("reports/completions/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportCompletionsReport(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] Guid? toolboxTalkId = null,
        [FromQuery] Guid? siteId = null)
    {
        try
        {
            // Get all completions (no pagination for export)
            var report = await _reportsService.GetCompletionReportAsync(
                _currentUserService.TenantId,
                dateFrom,
                dateTo,
                toolboxTalkId,
                siteId,
                1,
                10000); // Large page size for export

            var fileBytes = await _exportService.GenerateCompletionsReportExcelAsync(report.Items);

            if (fileBytes.Length == 0)
            {
                return BadRequest(Result.Fail("Export functionality is not yet implemented. Coming in Phase 2."));
            }

            var fileName = $"ToolboxTalkCompletions_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting completions report");
            return StatusCode(500, Result.Fail("Error exporting completions report"));
        }
    }

    /// <summary>
    /// Export compliance report as PDF
    /// </summary>
    /// <param name="dateFrom">Optional start date filter</param>
    /// <param name="dateTo">Optional end date filter</param>
    /// <param name="siteId">Optional site/department filter</param>
    /// <returns>PDF file</returns>
    [HttpGet("reports/compliance/export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportComplianceReport(
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] Guid? siteId = null)
    {
        try
        {
            var report = await _reportsService.GetComplianceReportAsync(
                _currentUserService.TenantId,
                dateFrom,
                dateTo,
                siteId);

            var fileBytes = await _exportService.GenerateComplianceReportPdfAsync(report);

            var fileName = $"ToolboxTalkCompliance_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            return File(fileBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting compliance report");
            return StatusCode(500, Result.Fail("Error exporting compliance report"));
        }
    }

    #endregion

    #region Certificates (Admin)

    /// <summary>
    /// Get certificate report with filtering, pagination, and summary stats
    /// </summary>
    /// <param name="status">Filter by status: valid, expired, expiring</param>
    /// <param name="type">Filter by type: Talk, Course</param>
    /// <param name="search">Search by employee name, training title, or employee code</param>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated certificate report with summary statistics</returns>
    [HttpGet("certificates/report")]
    [ProducesResponseType(typeof(Result<CertificateReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCertificateReport(
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var query = new GetCertificateReportQuery
            {
                TenantId = _currentUserService.TenantId,
                Status = status,
                Type = type,
                Search = search,
                Page = page,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving certificate report");
            return StatusCode(500, Result.Fail("Error retrieving certificate report"));
        }
    }

    /// <summary>
    /// Get all certificates for a specific employee (admin view)
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <returns>List of certificates ordered by issue date</returns>
    [HttpGet("certificates/by-employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(Result<List<CertificateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmployeeCertificates(Guid employeeId)
    {
        try
        {
            var query = new GetEmployeeCertificatesQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving certificates for employee {EmployeeId}", employeeId);
            return StatusCode(500, Result.Fail("Error retrieving employee certificates"));
        }
    }

    /// <summary>
    /// Download a certificate PDF (admin)
    /// </summary>
    /// <param name="id">Certificate ID</param>
    /// <returns>Certificate PDF file</returns>
    [HttpGet("certificates/{id:guid}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadCertificateAdmin(Guid id)
    {
        try
        {
            var query = new GetAdminCertificateDownloadQuery
            {
                TenantId = _currentUserService.TenantId,
                CertificateId = id
            };

            var result = await _mediator.Send(query);
            if (result == null)
            {
                return NotFound(new { message = "Certificate not found" });
            }

            var fileBytes = await _r2StorageService.DownloadFileAsync(result.StoragePath);
            if (fileBytes == null)
            {
                return NotFound(new { message = "Certificate file not found in storage" });
            }

            return File(fileBytes, "application/pdf", result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading certificate {CertificateId}", id);
            return StatusCode(500, new { message = "Error downloading certificate" });
        }
    }

    #endregion
}

/// <summary>
/// DTO for updating toolbox talk settings
/// </summary>
public record UpdateToolboxTalkSettingsDto
{
    public int DefaultDueDays { get; init; } = 7;
    public int ReminderDaysBefore { get; init; } = 3;
    public bool SendEmailReminders { get; init; } = true;
    public bool SendPushReminders { get; init; } = true;
    public int MaxQuizAttempts { get; init; } = 3;
    public bool RequireSignature { get; init; } = true;
    public bool AutoAssignNewEmployees { get; init; } = true;
}

/// <summary>
/// Request DTO for content extraction endpoint
/// </summary>
public record ExtractContentRequest
{
    /// <summary>
    /// Whether to extract and include video transcript content
    /// </summary>
    [Required]
    public bool IncludeVideo { get; init; }

    /// <summary>
    /// Whether to extract and include PDF document content
    /// </summary>
    [Required]
    public bool IncludePdf { get; init; }
}

/// <summary>
/// Request DTO for content generation endpoint
/// </summary>
public record GenerateContentRequest
{
    /// <summary>
    /// Whether to extract and use video transcript content
    /// </summary>
    [Required]
    public bool IncludeVideo { get; init; }

    /// <summary>
    /// Whether to extract and use PDF document content
    /// </summary>
    [Required]
    public bool IncludePdf { get; init; }

    /// <summary>
    /// Minimum number of sections to generate (default: 7)
    /// </summary>
    public int? MinimumSections { get; init; }

    /// <summary>
    /// Minimum number of quiz questions to generate (default: 5)
    /// </summary>
    public int? MinimumQuestions { get; init; }

    /// <summary>
    /// Quiz pass threshold percentage (default: 80)
    /// </summary>
    public int? PassThreshold { get; init; }

    /// <summary>
    /// Whether to replace existing sections and questions (default: true).
    /// If false, new content will be appended to existing.
    /// </summary>
    public bool? ReplaceExisting { get; init; }

    /// <summary>
    /// Optional SignalR connection ID for receiving real-time progress updates.
    /// Clients should connect to the ContentGenerationHub before calling this endpoint.
    /// </summary>
    public string? ConnectionId { get; init; }

    /// <summary>
    /// The language code of the original content (default: "en").
    /// Used for auto-translation to exclude the source language.
    /// </summary>
    public string? SourceLanguageCode { get; init; }

    /// <summary>
    /// Whether to auto-generate an animated slideshow from the PDF after content generation.
    /// </summary>
    public bool? GenerateSlidesFromPdf { get; init; }

    /// <summary>
    /// Slideshow source: "none", "pdf", or "video". Takes precedence over GenerateSlidesFromPdf when provided.
    /// </summary>
    public string? SlideshowSource { get; init; }
}

/// <summary>
/// Response DTO for content generation endpoint
/// </summary>
/// <param name="JobId">The Hangfire background job ID</param>
/// <param name="Message">Status message</param>
/// <param name="ToolboxTalkId">The toolbox talk ID being processed</param>
public record GenerateContentResponse(
    string JobId,
    string Message,
    Guid ToolboxTalkId);

/// <summary>
/// Request DTO for generating content translations
/// </summary>
public record GenerateTranslationsRequest
{
    /// <summary>
    /// List of language names to translate to (e.g., "Polish", "Romanian")
    /// </summary>
    public List<string> Languages { get; init; } = new();
}

/// <summary>
/// DTO for content translation information
/// </summary>
public class ContentTranslationDto
{
    /// <summary>
    /// ISO 639-1 language code
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the language
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// Translated title
    /// </summary>
    public string TranslatedTitle { get; set; } = string.Empty;

    /// <summary>
    /// When the translation was generated
    /// </summary>
    public DateTime TranslatedAt { get; set; }

    /// <summary>
    /// Provider used for translation (e.g., "Claude", "Manual")
    /// </summary>
    public string TranslationProvider { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO for checking duplicate content
/// </summary>
public record CheckDuplicateRequest
{
    /// <summary>
    /// The file hash (SHA-256) to check for duplicates.
    /// If not provided, fileUrl must be provided and the hash will be calculated.
    /// </summary>
    public string? FileHash { get; init; }

    /// <summary>
    /// The file URL to calculate hash from.
    /// Used if fileHash is not provided.
    /// </summary>
    public string? FileUrl { get; init; }

    /// <summary>
    /// Type of file: "PDF" or "Video"
    /// </summary>
    [Required]
    public string FileType { get; init; } = string.Empty;
}

/// <summary>
/// Response DTO for duplicate check
/// </summary>
public record DuplicateCheckResponse
{
    /// <summary>
    /// Whether a duplicate was found
    /// </summary>
    public bool IsDuplicate { get; init; }

    /// <summary>
    /// The calculated file hash
    /// </summary>
    public string FileHash { get; init; } = string.Empty;

    /// <summary>
    /// Information about the source toolbox talk if a duplicate was found
    /// </summary>
    public SourceToolboxTalkResponse? SourceToolboxTalk { get; init; }
}

/// <summary>
/// Response DTO for source toolbox talk information
/// </summary>
public record SourceToolboxTalkResponse
{
    /// <summary>
    /// ID of the source toolbox talk
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Title of the source toolbox talk
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// When the content was originally generated
    /// </summary>
    public DateTime? ProcessedAt { get; init; }

    /// <summary>
    /// Number of sections in the source
    /// </summary>
    public int SectionCount { get; init; }

    /// <summary>
    /// Number of questions in the source
    /// </summary>
    public int QuestionCount { get; init; }

    /// <summary>
    /// Whether the source has an HTML slideshow
    /// </summary>
    public bool HasSlideshow { get; init; }

    /// <summary>
    /// Languages that have translations available
    /// </summary>
    public List<string> TranslationLanguages { get; init; } = new();
}

/// <summary>
/// Request DTO for reusing content from another toolbox talk
/// </summary>
public record ReuseContentRequest
{
    /// <summary>
    /// ID of the source toolbox talk to copy content from
    /// </summary>
    [Required]
    public Guid SourceToolboxTalkId { get; init; }
}

/// <summary>
/// Response DTO for content reuse operation
/// </summary>
public record ContentReuseResponse
{
    /// <summary>
    /// Whether the reuse was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Number of sections copied
    /// </summary>
    public int SectionsCopied { get; init; }

    /// <summary>
    /// Number of questions copied
    /// </summary>
    public int QuestionsCopied { get; init; }

    /// <summary>
    /// Number of translations copied
    /// </summary>
    public int TranslationsCopied { get; init; }

    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Request DTO for updating file hash
/// </summary>
public record UpdateFileHashRequest
{
    /// <summary>
    /// The file hash (SHA-256).
    /// If not provided, fileUrl must be provided and the hash will be calculated.
    /// </summary>
    public string? FileHash { get; init; }

    /// <summary>
    /// The file URL to calculate hash from.
    /// Used if fileHash is not provided.
    /// </summary>
    public string? FileUrl { get; init; }

    /// <summary>
    /// Type of file: "PDF" or "Video"
    /// </summary>
    [Required]
    public string FileType { get; init; } = string.Empty;
}

/// <summary>
/// Response DTO for slideshow generation
/// </summary>
public record GenerateSlidesResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int HtmlLength { get; init; }
}

/// <summary>
/// Request DTO for smart content generation
/// </summary>
public record SmartGenerateContentRequest
{
    /// <summary>Whether to generate/reuse sections</summary>
    public bool GenerateSections { get; init; } = true;

    /// <summary>Whether to generate/reuse quiz questions</summary>
    public bool GenerateQuestions { get; init; } = true;

    /// <summary>Whether to generate/reuse HTML slideshow</summary>
    public bool GenerateSlideshow { get; init; }

    /// <summary>Whether to include video as a content source</summary>
    public bool IncludeVideo { get; init; }

    /// <summary>Whether to include PDF as a content source</summary>
    public bool IncludePdf { get; init; }

    /// <summary>The language code of the original content (default: "en")</summary>
    public string? SourceLanguageCode { get; init; }

    /// <summary>Minimum number of sections to generate</summary>
    public int? MinimumSections { get; init; }

    /// <summary>Minimum number of quiz questions to generate</summary>
    public int? MinimumQuestions { get; init; }

    /// <summary>Quiz pass threshold percentage</summary>
    public int? PassThreshold { get; init; }

    /// <summary>SignalR connection ID for progress updates during AI generation</summary>
    public string? ConnectionId { get; init; }

    /// <summary>Slideshow source: "none", "pdf", or "video"</summary>
    public string? SlideshowSource { get; init; }
}

/// <summary>
/// Response DTO for smart content generation
/// </summary>
public record SmartGenerateContentResponse
{
    // What was copied from existing source
    public int SectionsCopied { get; init; }
    public int QuestionsCopied { get; init; }
    public bool SlideshowCopied { get; init; }
    public int TranslationsCopied { get; init; }
    public bool SubtitlesCopied { get; init; }

    // What was generated via AI (0 if generation is queued as background job)
    public int SectionsGenerated { get; init; }
    public int QuestionsGenerated { get; init; }
    public bool SlideshowGenerated { get; init; }

    // Source info
    public string? ContentCopiedFromTitle { get; init; }

    // Background job info (if AI generation was needed)
    public bool GenerationJobQueued { get; init; }
    public string? GenerationJobId { get; init; }

    // Missing translations job info (if content was fully reused but some languages are missing)
    public bool MissingTranslationsJobQueued { get; init; }
    public string? MissingTranslationsJobId { get; init; }
}
