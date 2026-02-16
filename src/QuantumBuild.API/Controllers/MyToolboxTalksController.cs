using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.CompleteToolboxTalk;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.MarkSectionRead;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.StartToolboxTalk;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.ResetVideoProgress;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.SubmitQuizAnswers;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateVideoProgress;
using QuantumBuild.Modules.ToolboxTalks.Application.Abstractions.Storage;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Certificates.Queries;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.CourseAssignments.Queries;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetMyToolboxTalkById;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetMyToolboxTalks;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetSlideshowHtml;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkSlides;
using QuantumBuild.Modules.ToolboxTalks.Application.Services.Subtitles;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for employee portal to view and complete assigned toolbox talks
/// All endpoints return data filtered to the current authenticated employee
/// </summary>
[ApiController]
[Route("api/my/toolbox-talks")]
[Authorize]
public class MyToolboxTalksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISubtitleProcessingOrchestrator _subtitleOrchestrator;
    private readonly IR2StorageService _r2StorageService;
    private readonly ILogger<MyToolboxTalksController> _logger;

    public MyToolboxTalksController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        ISubtitleProcessingOrchestrator subtitleOrchestrator,
        IR2StorageService r2StorageService,
        ILogger<MyToolboxTalksController> logger)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _subtitleOrchestrator = subtitleOrchestrator;
        _r2StorageService = r2StorageService;
        _logger = logger;
    }

    /// <summary>
    /// Get all toolbox talks assigned to the current employee
    /// </summary>
    /// <param name="status">Optional filter by status</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of assigned toolbox talks</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<MyToolboxTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTalks(
        [FromQuery] ScheduledTalkStatus? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var query = new GetMyToolboxTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                Status = status,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving my toolbox talks");
            return StatusCode(500, Result.Fail("Error retrieving my toolbox talks"));
        }
    }

    /// <summary>
    /// Get a specific assigned toolbox talk with full content for completion
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <returns>Full toolbox talk content with progress</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MyToolboxTalkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyTalkById(Guid id)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var query = new GetMyToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                ScheduledTalkId = id
            };

            var result = await _mediator.Send(query);
            if (result == null)
            {
                return NotFound(new { message = "Toolbox talk not found or not assigned to you" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving my toolbox talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error retrieving toolbox talk" });
        }
    }

    /// <summary>
    /// Record that the employee has started viewing a toolbox talk.
    /// Captures optional geolocation and transitions status to InProgress.
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <param name="request">Optional location data</param>
    /// <returns>200 OK</returns>
    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Start(Guid id, [FromBody] StartTalkRequest? request = null)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var command = new StartToolboxTalkCommand
            {
                ScheduledTalkId = id,
                Latitude = request?.Latitude,
                Longitude = request?.Longitude,
                AccuracyMeters = request?.AccuracyMeters
            };

            await _mediator.Send(command);
            return Ok(new { message = "Talk started" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error starting toolbox talk" });
        }
    }

    /// <summary>
    /// Mark a section as read
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <param name="sectionId">Section ID to mark as read</param>
    /// <param name="request">Optional time spent data</param>
    /// <returns>Updated section progress</returns>
    [HttpPost("{id:guid}/sections/{sectionId:guid}/read")]
    [ProducesResponseType(typeof(ScheduledTalkSectionProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkSectionRead(
        Guid id,
        Guid sectionId,
        [FromBody] MarkSectionReadRequest? request = null)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var command = new MarkSectionReadCommand
            {
                ScheduledTalkId = id,
                SectionId = sectionId,
                TimeSpentSeconds = request?.TimeSpentSeconds
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking section {SectionId} as read for talk {ScheduledTalkId}", sectionId, id);
            return StatusCode(500, new { message = "Error marking section as read" });
        }
    }

    /// <summary>
    /// Submit quiz answers for a toolbox talk
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <param name="request">Quiz answers</param>
    /// <returns>Quiz result with score and pass/fail status</returns>
    [HttpPost("{id:guid}/quiz/submit")]
    [ProducesResponseType(typeof(QuizResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitQuiz(Guid id, [FromBody] SubmitQuizRequest request)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var command = new SubmitQuizAnswersCommand
            {
                ScheduledTalkId = id,
                Answers = request.Answers
            };

            var result = await _mediator.Send(command);
            return Ok(result);
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
            _logger.LogError(ex, "Error submitting quiz for talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error submitting quiz" });
        }
    }

    /// <summary>
    /// Update video watch progress
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <param name="request">Video progress data</param>
    /// <returns>Updated progress status</returns>
    [HttpPost("{id:guid}/video-progress")]
    [ProducesResponseType(typeof(VideoProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateVideoProgress(Guid id, [FromBody] UpdateVideoProgressRequest request)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var command = new UpdateVideoProgressCommand
            {
                ScheduledTalkId = id,
                WatchPercent = request.WatchPercent
            };

            var result = await _mediator.Send(command);
            return Ok(result);
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
            _logger.LogError(ex, "Error updating video progress for talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error updating video progress" });
        }
    }

    /// <summary>
    /// Reset video watch progress (e.g., when rewatching after a failed quiz)
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <returns>Reset progress status</returns>
    [HttpPost("{id:guid}/reset-video-progress")]
    [ProducesResponseType(typeof(VideoProgressDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetVideoProgress(Guid id)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var command = new ResetVideoProgressCommand
            {
                ScheduledTalkId = id
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting video progress for talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error resetting video progress" });
        }
    }

    /// <summary>
    /// Complete a toolbox talk with signature
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <param name="request">Completion data including signature</param>
    /// <returns>Completion confirmation with certificate details</returns>
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(typeof(ScheduledTalkCompletionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteToolboxTalkRequest request)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var command = new CompleteToolboxTalkCommand
            {
                ScheduledTalkId = id,
                SignatureData = request.SignatureData,
                SignedByName = request.SignedByName,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                AccuracyMeters = request.AccuracyMeters
            };

            var result = await _mediator.Send(command);
            return Ok(result);
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
            _logger.LogError(ex, "Error completing talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error completing toolbox talk" });
        }
    }

    /// <summary>
    /// Get pending (not started) toolbox talks for the current employee
    /// </summary>
    /// <returns>List of pending toolbox talks</returns>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(PaginatedList<MyToolboxTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var query = new GetMyToolboxTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                Status = ScheduledTalkStatus.Pending,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending toolbox talks");
            return StatusCode(500, Result.Fail("Error retrieving pending toolbox talks"));
        }
    }

    /// <summary>
    /// Get in-progress toolbox talks for the current employee
    /// </summary>
    /// <returns>List of in-progress toolbox talks</returns>
    [HttpGet("in-progress")]
    [ProducesResponseType(typeof(PaginatedList<MyToolboxTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInProgress(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var query = new GetMyToolboxTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                Status = ScheduledTalkStatus.InProgress,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving in-progress toolbox talks");
            return StatusCode(500, Result.Fail("Error retrieving in-progress toolbox talks"));
        }
    }

    /// <summary>
    /// Get overdue toolbox talks for the current employee
    /// </summary>
    /// <returns>List of overdue toolbox talks</returns>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(PaginatedList<MyToolboxTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdue(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var query = new GetMyToolboxTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                Status = ScheduledTalkStatus.Overdue,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue toolbox talks");
            return StatusCode(500, Result.Fail("Error retrieving overdue toolbox talks"));
        }
    }

    /// <summary>
    /// Get completed toolbox talks for the current employee
    /// </summary>
    /// <returns>List of completed toolbox talks</returns>
    [HttpGet("completed")]
    [ProducesResponseType(typeof(PaginatedList<MyToolboxTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompleted(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var query = new GetMyToolboxTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                Status = ScheduledTalkStatus.Completed,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving completed toolbox talks");
            return StatusCode(500, Result.Fail("Error retrieving completed toolbox talks"));
        }
    }

    /// <summary>
    /// Get summary counts for the current employee's pending and overdue toolbox talks
    /// </summary>
    /// <returns>Summary with pending and overdue counts</returns>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(MyTrainingSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary()
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return Ok(new MyTrainingSummaryDto { PendingCount = 0, OverdueCount = 0, InProgressCount = 0 });
            }

            // Get pending talks
            var pendingQuery = new GetMyToolboxTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                Status = ScheduledTalkStatus.Pending,
                PageNumber = 1,
                PageSize = 1
            };
            var pendingResult = await _mediator.Send(pendingQuery);

            // Get in-progress talks
            var inProgressQuery = new GetMyToolboxTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                Status = ScheduledTalkStatus.InProgress,
                PageNumber = 1,
                PageSize = 1
            };
            var inProgressResult = await _mediator.Send(inProgressQuery);

            // Get overdue talks
            var overdueQuery = new GetMyToolboxTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                Status = ScheduledTalkStatus.Overdue,
                PageNumber = 1,
                PageSize = 1
            };
            var overdueResult = await _mediator.Send(overdueQuery);

            return Ok(new MyTrainingSummaryDto
            {
                PendingCount = pendingResult.TotalCount,
                InProgressCount = inProgressResult.TotalCount,
                OverdueCount = overdueResult.TotalCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving toolbox talks summary");
            return StatusCode(500, new { message = "Error retrieving summary" });
        }
    }

    /// <summary>
    /// Get subtitle processing status for an assigned toolbox talk
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <returns>Subtitle processing status</returns>
    [HttpGet("{id:guid}/subtitles/status")]
    [ProducesResponseType(typeof(SubtitleProcessingStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubtitleStatus(Guid id)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            // First verify the employee is assigned to this talk
            var query = new GetMyToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                ScheduledTalkId = id
            };

            var talk = await _mediator.Send(query);
            if (talk == null)
            {
                return NotFound(new { message = "Toolbox talk not found or not assigned to you" });
            }

            // Get subtitle status using the actual toolbox talk ID
            var status = await _subtitleOrchestrator.GetStatusAsync(talk.ToolboxTalkId);
            if (status == null)
            {
                return NotFound(new { message = "No subtitles available for this toolbox talk" });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subtitle status for talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error getting subtitle status" });
        }
    }

    /// <summary>
    /// Get subtitle file for an assigned toolbox talk
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <param name="languageCode">ISO 639-1 language code (e.g., 'en', 'es', 'pl')</param>
    /// <param name="format">Format: 'srt' (default) or 'vtt' (WebVTT for browser video players)</param>
    /// <returns>Subtitle file content</returns>
    [HttpGet("{id:guid}/subtitles/{languageCode}")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "text/vtt")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "application/x-subrip")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSubtitleFile(
        Guid id,
        string languageCode,
        [FromQuery] string format = "srt")
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            // First verify the employee is assigned to this talk
            var query = new GetMyToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                ScheduledTalkId = id
            };

            var talk = await _mediator.Send(query);
            if (talk == null)
            {
                return NotFound(new { message = "Toolbox talk not found or not assigned to you" });
            }

            // Get subtitle content using the actual toolbox talk ID
            var srtContent = await _subtitleOrchestrator.GetSrtContentAsync(talk.ToolboxTalkId, languageCode);
            if (srtContent == null)
            {
                return NotFound(new { message = $"No subtitle found for language code '{languageCode}'" });
            }

            string content;
            string contentType;

            if (format.Equals("vtt", StringComparison.OrdinalIgnoreCase))
            {
                content = ConvertSrtToVtt(srtContent);
                contentType = "text/vtt; charset=utf-8";
            }
            else
            {
                content = srtContent;
                contentType = "application/x-subrip; charset=utf-8";
            }

            // Return Content directly instead of Ok() to bypass JSON content negotiation
            return Content(content, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subtitle file for talk {ScheduledTalkId}, language {Language}", id, languageCode);
            return StatusCode(500, new { message = "Error getting subtitle file" });
        }
    }

    /// <summary>
    /// Converts SRT subtitle format to WebVTT format for browser video players
    /// </summary>
    private static string ConvertSrtToVtt(string srtContent)
    {
        var lines = srtContent.Split('\n');
        var vttLines = new List<string> { "WEBVTT", "" };

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd('\r');

            // Convert timestamp format: 00:00:00,000 --> 00:00:00.000
            if (trimmedLine.Contains(" --> "))
            {
                vttLines.Add(trimmedLine.Replace(',', '.'));
            }
            else
            {
                vttLines.Add(trimmedLine);
            }
        }

        return string.Join("\n", vttLines);
    }

    /// <summary>
    /// Get slides for an assigned toolbox talk with optional translated text
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <param name="lang">Optional ISO 639-1 language code for translated slide text</param>
    /// <returns>List of slides with image URLs and text</returns>
    [HttpGet("{id:guid}/slides")]
    [ProducesResponseType(typeof(List<SlideDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSlides(Guid id, [FromQuery] string? lang)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            // Verify the employee is assigned to this talk
            var query = new GetMyToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                ScheduledTalkId = id
            };

            var talk = await _mediator.Send(query);
            if (talk == null)
            {
                return NotFound(new { message = "Toolbox talk not found or not assigned to you" });
            }

            // Use employee's preferred language if no language specified
            var languageCode = lang ?? talk.LanguageCode;

            var slidesQuery = new GetToolboxTalkSlidesQuery
            {
                TenantId = _currentUserService.TenantId,
                ToolboxTalkId = talk.ToolboxTalkId,
                LanguageCode = languageCode
            };

            var slides = await _mediator.Send(slidesQuery);
            return Ok(slides);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slides for talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error getting slides" });
        }
    }

    /// <summary>
    /// Get slideshow HTML for an assigned toolbox talk with optional translation.
    /// Returns the AI-generated HTML slideshow, optionally translated to the employee's preferred language.
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <param name="lang">Optional ISO 639-1 language code for translated slideshow</param>
    /// <returns>Slideshow HTML with translation metadata</returns>
    [HttpGet("{id:guid}/slideshow")]
    [ProducesResponseType(typeof(SlideshowHtmlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSlideshowHtml(Guid id, [FromQuery] string? lang)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            // Verify the employee is assigned to this talk
            var talkQuery = new GetMyToolboxTalkByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                ScheduledTalkId = id
            };

            var talk = await _mediator.Send(talkQuery);
            if (talk == null)
            {
                return NotFound(new { message = "Toolbox talk not found or not assigned to you" });
            }

            // Default to employee's preferred language if not specified
            var languageCode = lang ?? talk.EmployeePreferredLanguage;

            var result = await _mediator.Send(new GetSlideshowHtmlQuery
            {
                TenantId = _currentUserService.TenantId,
                ToolboxTalkId = talk.ToolboxTalkId,
                LanguageCode = languageCode
            });

            if (!result.Success)
                return BadRequest(new { error = string.Join("; ", result.Errors) });

            return Ok(result.Data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting slideshow HTML for talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error getting slideshow" });
        }
    }

    /// <summary>
    /// Get course assignments for the current employee
    /// </summary>
    [HttpGet("courses")]
    [ProducesResponseType(typeof(Result<List<ToolboxTalkCourseAssignmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCourses()
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var query = new GetEmployeeCourseAssignmentsQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving my course assignments");
            return StatusCode(500, Result.Fail("Error retrieving my course assignments"));
        }
    }

    /// <summary>
    /// Get a specific course assignment for the current employee
    /// </summary>
    [HttpGet("courses/{id:guid}")]
    [ProducesResponseType(typeof(ToolboxTalkCourseAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyCourseAssignment(Guid id)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var query = new GetMyCourseAssignmentByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
                Id = id
            };

            var result = await _mediator.Send(query);
            if (result == null)
            {
                return NotFound(new { message = "Course assignment not found or not assigned to you" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving my course assignment {AssignmentId}", id);
            return StatusCode(500, new { message = "Error retrieving course assignment" });
        }
    }

    /// <summary>
    /// Get all certificates for the current employee
    /// </summary>
    /// <returns>List of certificates ordered by issue date</returns>
    [HttpGet("certificates")]
    [ProducesResponseType(typeof(Result<List<CertificateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCertificates()
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var query = new GetMyCertificatesQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving my certificates");
            return StatusCode(500, Result.Fail("Error retrieving my certificates"));
        }
    }

    /// <summary>
    /// Download a certificate PDF
    /// </summary>
    /// <param name="id">Certificate ID</param>
    /// <returns>Certificate PDF file</returns>
    [HttpGet("certificates/{id:guid}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadCertificate(Guid id)
    {
        try
        {
            var employeeId = GetCurrentEmployeeId();
            if (employeeId == null)
            {
                return BadRequest(new { message = "No employee record associated with current user" });
            }

            var query = new GetCertificateDownloadQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId.Value,
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

    /// <summary>
    /// Get current employee ID from claims
    /// </summary>
    private Guid? GetCurrentEmployeeId()
    {
        var employeeIdClaim = User.FindFirst("employee_id")?.Value;
        if (string.IsNullOrEmpty(employeeIdClaim) || !Guid.TryParse(employeeIdClaim, out var employeeId))
        {
            return null;
        }
        return employeeId;
    }
}

#region Request DTOs

/// <summary>
/// Request for marking a section as read
/// </summary>
public record MarkSectionReadRequest
{
    /// <summary>
    /// Optional time spent on this section in seconds
    /// </summary>
    public int? TimeSpentSeconds { get; init; }
}

/// <summary>
/// Request for submitting quiz answers
/// </summary>
public record SubmitQuizRequest
{
    /// <summary>
    /// Dictionary of question ID to submitted answer
    /// </summary>
    public Dictionary<Guid, string> Answers { get; init; } = new();
}

/// <summary>
/// Request for updating video watch progress
/// </summary>
public record UpdateVideoProgressRequest
{
    /// <summary>
    /// Current watch progress percentage (0-100)
    /// </summary>
    public int WatchPercent { get; init; }
}

/// <summary>
/// Request for starting a toolbox talk with optional geolocation
/// </summary>
public record StartTalkRequest
{
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? AccuracyMeters { get; init; }
}

/// <summary>
/// Request for completing a toolbox talk
/// </summary>
public record CompleteToolboxTalkRequest
{
    /// <summary>
    /// Base64 encoded signature image (PNG)
    /// </summary>
    public string SignatureData { get; init; } = string.Empty;

    /// <summary>
    /// Name entered by the employee when signing
    /// </summary>
    public string SignedByName { get; init; } = string.Empty;

    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? AccuracyMeters { get; init; }
}

#endregion

#region Response DTOs

/// <summary>
/// Summary of pending and overdue toolbox talks for the current employee
/// </summary>
public record MyTrainingSummaryDto
{
    /// <summary>
    /// Number of pending (not started) toolbox talks
    /// </summary>
    public int PendingCount { get; init; }

    /// <summary>
    /// Number of in-progress toolbox talks
    /// </summary>
    public int InProgressCount { get; init; }

    /// <summary>
    /// Number of overdue toolbox talks
    /// </summary>
    public int OverdueCount { get; init; }

    /// <summary>
    /// Total number of talks requiring attention (pending + in-progress + overdue)
    /// </summary>
    public int TotalCount => PendingCount + InProgressCount + OverdueCount;
}

#endregion
