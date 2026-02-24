using FluentValidation;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.LessonParser.Application.Abstractions;
using QuantumBuild.Modules.LessonParser.Application.Common.Interfaces;
using QuantumBuild.Modules.LessonParser.Application.DTOs;
using QuantumBuild.Modules.LessonParser.Application.Validators;
using QuantumBuild.Modules.LessonParser.Domain.Entities;
using QuantumBuild.Modules.LessonParser.Domain.Enums;
using QuantumBuild.Modules.LessonParser.Infrastructure.Jobs;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for the Lesson Parser module — accepts documents, extracts content,
/// and generates courses with talks via AI background processing.
/// </summary>
[ApiController]
[Route("api/lesson-parser")]
[Authorize(Policy = "LessonParser.Use")]
public class LessonParserController : ControllerBase
{
    private readonly ILessonParserDbContext _dbContext;
    private readonly IDocumentExtractor _documentExtractor;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LessonParserController> _logger;

    private const long MaxDocumentSizeBytes = 50 * 1024 * 1024; // 50MB

    private static readonly HashSet<string> SupportedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx"
    };

    public LessonParserController(
        ILessonParserDbContext dbContext,
        IDocumentExtractor documentExtractor,
        ICurrentUserService currentUserService,
        ILogger<LessonParserController> logger)
    {
        _dbContext = dbContext;
        _documentExtractor = documentExtractor;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    #region Parse Endpoints

    /// <summary>
    /// Parse a PDF or Word document into a course with talks.
    /// Detects the file type from the extension and routes to the correct extractor.
    /// </summary>
    /// <param name="file">PDF or DOCX file (max 50MB)</param>
    /// <param name="connectionId">SignalR connection ID for progress updates</param>
    /// <returns>Parse job information</returns>
    [HttpPost("parse/document")]
    [RequestSizeLimit(52428800)] // 50MB
    [ProducesResponseType(typeof(StartParseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseDocument(
        IFormFile file,
        [FromQuery] string? connectionId = null)
    {
        try
        {
            // Validate file
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !SupportedDocumentExtensions.Contains(extension))
                return BadRequest(new { error = "Unsupported file type. Please upload a PDF or Word document (.pdf, .docx)" });

            if (file.Length > MaxDocumentSizeBytes)
                return BadRequest(new { error = $"File size ({file.Length / 1024 / 1024}MB) exceeds maximum ({MaxDocumentSizeBytes / 1024 / 1024}MB)" });

            // Detect input type and extract content
            await using var stream = file.OpenReadStream();
            var (inputType, extractionResult) = extension switch
            {
                ".pdf" => (ParseInputType.Pdf, await _documentExtractor.ExtractFromPdfAsync(stream, file.FileName)),
                ".docx" => (ParseInputType.Docx, await _documentExtractor.ExtractFromDocxAsync(stream, file.FileName)),
                _ => throw new InvalidOperationException($"Unhandled extension: {extension}")
            };

            if (extractionResult.IsEmpty)
                return BadRequest(new { error = $"Could not extract text from the {extension} file" });

            // Create and enqueue job
            return await CreateAndEnqueueJobAsync(
                inputType,
                file.FileName,
                extractionResult,
                connectionId);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error parsing document {FileName}", file?.FileName);
            return StatusCode(500, new { error = "Error processing document" });
        }
    }

    /// <summary>
    /// Parse a PDF document into a course with talks
    /// </summary>
    [HttpPost("parse/pdf")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [RequestSizeLimit(52428800)] // 50MB
    [ProducesResponseType(typeof(StartParseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParsePdf(
        IFormFile file,
        [FromQuery] string? connectionId = null)
    {
        try
        {
            // Validate file
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (extension != ".pdf")
                return BadRequest(new { error = "File must be a PDF (.pdf)" });

            if (file.Length > MaxDocumentSizeBytes)
                return BadRequest(new { error = $"File size ({file.Length / 1024 / 1024}MB) exceeds maximum ({MaxDocumentSizeBytes / 1024 / 1024}MB)" });

            // Extract content
            await using var stream = file.OpenReadStream();
            var extractionResult = await _documentExtractor.ExtractFromPdfAsync(stream, file.FileName);

            if (extractionResult.IsEmpty)
                return BadRequest(new { error = "Could not extract text from the PDF" });

            // Create and enqueue job
            return await CreateAndEnqueueJobAsync(
                ParseInputType.Pdf,
                file.FileName,
                extractionResult,
                connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PDF file {FileName}", file?.FileName);
            return StatusCode(500, new { error = "Error processing PDF file" });
        }
    }

    /// <summary>
    /// Parse a DOCX document into a course with talks
    /// </summary>
    [HttpPost("parse/docx")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [RequestSizeLimit(52428800)] // 50MB
    [ProducesResponseType(typeof(StartParseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseDocx(
        IFormFile file,
        [FromQuery] string? connectionId = null)
    {
        try
        {
            // Validate file
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file provided" });

            var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (extension != ".docx")
                return BadRequest(new { error = "File must be a Word document (.docx)" });

            if (file.Length > MaxDocumentSizeBytes)
                return BadRequest(new { error = $"File size ({file.Length / 1024 / 1024}MB) exceeds maximum ({MaxDocumentSizeBytes / 1024 / 1024}MB)" });

            // Extract content
            await using var stream = file.OpenReadStream();
            var extractionResult = await _documentExtractor.ExtractFromDocxAsync(stream, file.FileName);

            if (extractionResult.IsEmpty)
                return BadRequest(new { error = "Could not extract text from the DOCX file" });

            // Create and enqueue job
            return await CreateAndEnqueueJobAsync(
                ParseInputType.Docx,
                file.FileName,
                extractionResult,
                connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing DOCX file {FileName}", file?.FileName);
            return StatusCode(500, new { error = "Error processing DOCX file" });
        }
    }

    /// <summary>
    /// Parse content from a URL into a course with talks
    /// </summary>
    /// <param name="request">URL to parse</param>
    /// <param name="connectionId">SignalR connection ID for progress updates</param>
    /// <returns>Parse job information</returns>
    [HttpPost("parse/url")]
    [ProducesResponseType(typeof(StartParseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseUrl(
        [FromBody] SubmitUrlRequest request,
        [FromQuery] string? connectionId = null)
    {
        try
        {
            // Validate
            var validator = new SubmitUrlRequestValidator();
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
                return BadRequest(new { error = validationResult.Errors.First().ErrorMessage });

            // Extract content
            var extractionResult = await _documentExtractor.ExtractFromUrlAsync(request.Url);

            if (extractionResult.IsEmpty)
                return BadRequest(new { error = "Could not extract text from the URL" });

            // Create and enqueue job
            return await CreateAndEnqueueJobAsync(
                ParseInputType.Url,
                request.Url,
                extractionResult,
                connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing URL {Url}", request?.Url);
            return StatusCode(500, new { error = "Error processing URL" });
        }
    }

    /// <summary>
    /// Parse raw text content into a course with talks
    /// </summary>
    /// <param name="request">Text content and title</param>
    /// <param name="connectionId">SignalR connection ID for progress updates</param>
    /// <returns>Parse job information</returns>
    [HttpPost("parse/text")]
    [ProducesResponseType(typeof(StartParseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseText(
        [FromBody] SubmitTextRequest request,
        [FromQuery] string? connectionId = null)
    {
        try
        {
            // Validate
            var validator = new SubmitTextRequestValidator();
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
                return BadRequest(new { error = validationResult.Errors.First().ErrorMessage });

            // Extract content
            var extractionResult = await _documentExtractor.ExtractFromTextAsync(
                request.Content, request.Title);

            if (extractionResult.IsEmpty)
                return BadRequest(new { error = "Could not process the text content" });

            // Create and enqueue job
            return await CreateAndEnqueueJobAsync(
                ParseInputType.Text,
                request.Title,
                extractionResult,
                connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing text content");
            return StatusCode(500, new { error = "Error processing text content" });
        }
    }

    #endregion

    #region Job Management Endpoints

    /// <summary>
    /// Get paginated list of parse jobs for the current tenant
    /// </summary>
    /// <param name="page">Page number (1-based, default: 1)</param>
    /// <param name="pageSize">Page size (default: 10)</param>
    /// <returns>Paginated list of parse jobs</returns>
    [HttpGet("jobs")]
    [ProducesResponseType(typeof(PaginatedList<ParseJobDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = _dbContext.ParseJobs
                .OrderByDescending(j => j.CreatedAt)
                .Select(j => new ParseJobDto
                {
                    Id = j.Id,
                    InputType = j.InputType.ToString(),
                    InputReference = j.InputReference,
                    Status = j.Status.ToString(),
                    GeneratedCourseId = j.GeneratedCourseId,
                    GeneratedCourseTitle = j.GeneratedCourseTitle,
                    TalksGenerated = j.TalksGenerated,
                    ErrorMessage = j.ErrorMessage,
                    TranslationStatus = j.TranslationStatus.ToString(),
                    TranslationLanguages = j.TranslationLanguages,
                    TranslationsQueued = j.TranslationsQueued,
                    TranslationFailures = j.TranslationFailures,
                    CreatedAt = j.CreatedAt,
                    CreatedBy = j.CreatedBy
                });

            var result = await PaginatedList<ParseJobDto>.CreateAsync(query, page, pageSize);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving parse jobs");
            return StatusCode(500, Result.Fail("Error retrieving parse jobs"));
        }
    }

    /// <summary>
    /// Get a single parse job by ID
    /// </summary>
    /// <param name="id">Parse job ID</param>
    /// <returns>Parse job details</returns>
    [HttpGet("jobs/{id:guid}")]
    [ProducesResponseType(typeof(ParseJobDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJobById(Guid id)
    {
        try
        {
            var job = await _dbContext.ParseJobs
                .Where(j => j.Id == id)
                .Select(j => new ParseJobDto
                {
                    Id = j.Id,
                    InputType = j.InputType.ToString(),
                    InputReference = j.InputReference,
                    Status = j.Status.ToString(),
                    GeneratedCourseId = j.GeneratedCourseId,
                    GeneratedCourseTitle = j.GeneratedCourseTitle,
                    TalksGenerated = j.TalksGenerated,
                    ErrorMessage = j.ErrorMessage,
                    TranslationStatus = j.TranslationStatus.ToString(),
                    TranslationLanguages = j.TranslationLanguages,
                    TranslationsQueued = j.TranslationsQueued,
                    TranslationFailures = j.TranslationFailures,
                    CreatedAt = j.CreatedAt,
                    CreatedBy = j.CreatedBy
                })
                .FirstOrDefaultAsync();

            if (job == null)
                return NotFound(new { message = "Parse job not found" });

            return Ok(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving parse job {ParseJobId}", id);
            return StatusCode(500, new { error = "Error retrieving parse job" });
        }
    }

    /// <summary>
    /// Retry a failed parse job. Re-enqueues the job using the stored extracted content.
    /// </summary>
    /// <param name="id">Parse job ID</param>
    /// <param name="connectionId">SignalR connection ID for progress updates</param>
    /// <returns>New parse response</returns>
    [HttpPost("jobs/{id:guid}/retry")]
    [Authorize(Policy = "LessonParser.Admin")]
    [ProducesResponseType(typeof(StartParseResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RetryJob(
        Guid id,
        [FromQuery] string? connectionId = null)
    {
        try
        {
            var parseJob = await _dbContext.ParseJobs
                .FirstOrDefaultAsync(j => j.Id == id);

            if (parseJob == null)
                return NotFound(new { message = "Parse job not found" });

            if (parseJob.Status != ParseJobStatus.Failed)
                return BadRequest(new { error = "Only failed jobs can be retried" });

            if (string.IsNullOrEmpty(parseJob.ExtractedContent))
                return BadRequest(new { error = "Extracted content is no longer available for retry. Please submit the document again." });

            // Reset job status
            parseJob.Status = ParseJobStatus.Processing;
            parseJob.ErrorMessage = null;
            parseJob.GeneratedCourseId = null;
            parseJob.GeneratedCourseTitle = null;
            parseJob.TalksGenerated = 0;
            await _dbContext.SaveChangesAsync(CancellationToken.None);

            // Re-enqueue with stored content
            BackgroundJob.Enqueue<LessonParseJob>(j =>
                j.ExecuteAsync(
                    parseJob.Id,
                    parseJob.ExtractedContent,
                    parseJob.InputReference,
                    connectionId ?? string.Empty));

            _logger.LogInformation(
                "Retried parse job {ParseJobId} for input {InputReference}",
                parseJob.Id, parseJob.InputReference);

            return Ok(new StartParseResponse
            {
                JobId = parseJob.Id,
                Message = "Parse job retried. Connect to the SignalR hub for real-time progress updates."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying parse job {ParseJobId}", id);
            return StatusCode(500, new { error = "Error retrying parse job" });
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Creates a ParseJob entity, saves it, and enqueues the Hangfire background job.
    /// </summary>
    private async Task<IActionResult> CreateAndEnqueueJobAsync(
        ParseInputType inputType,
        string inputReference,
        ExtractionResult extractionResult,
        string? connectionId)
    {
        var parseJob = new ParseJob
        {
            InputType = inputType,
            InputReference = inputReference,
            Status = ParseJobStatus.Processing,
            ExtractedContent = extractionResult.Content,
            TenantId = _currentUserService.TenantId,
            CreatedBy = _currentUserService.UserId ?? "system"
        };

        _dbContext.ParseJobs.Add(parseJob);
        await _dbContext.SaveChangesAsync(CancellationToken.None);

        // Enqueue Hangfire background job
        BackgroundJob.Enqueue<LessonParseJob>(j =>
            j.ExecuteAsync(
                parseJob.Id,
                extractionResult.Content,
                extractionResult.Title,
                connectionId ?? string.Empty));

        _logger.LogInformation(
            "Enqueued parse job {ParseJobId} for {InputType}: {InputReference}",
            parseJob.Id, inputType, inputReference);

        return Ok(new StartParseResponse
        {
            JobId = parseJob.Id,
            Message = "Parse job started. Connect to the SignalR hub for real-time progress updates."
        });
    }

    #endregion
}
