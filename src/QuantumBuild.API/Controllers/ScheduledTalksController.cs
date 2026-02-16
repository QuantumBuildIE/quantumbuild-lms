using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.CancelScheduledTalk;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.SendScheduledTalkReminder;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetScheduledTalks;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for viewing scheduled talk assignments (admin view)
/// Provides read-only access to all employee assignments
/// </summary>
[ApiController]
[Route("api/toolbox-talks/assigned")]
[Authorize(Policy = "ToolboxTalks.View")]
public class ScheduledTalksController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ScheduledTalksController> _logger;

    public ScheduledTalksController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        ILogger<ScheduledTalksController> logger)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get all scheduled talk assignments with pagination and filtering
    /// </summary>
    /// <param name="employeeId">Optional filter by employee</param>
    /// <param name="toolboxTalkId">Optional filter by toolbox talk</param>
    /// <param name="status">Optional filter by status</param>
    /// <param name="dueDateFrom">Optional filter for due dates from this date</param>
    /// <param name="dueDateTo">Optional filter for due dates until this date</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of scheduled talk assignments</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ScheduledTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? employeeId = null,
        [FromQuery] Guid? toolboxTalkId = null,
        [FromQuery] ScheduledTalkStatus? status = null,
        [FromQuery] DateTime? dueDateFrom = null,
        [FromQuery] DateTime? dueDateTo = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = new GetScheduledTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId,
                ToolboxTalkId = toolboxTalkId,
                Status = status,
                DueDateFrom = dueDateFrom,
                DueDateTo = dueDateTo,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scheduled talks");
            return StatusCode(500, Result.Fail("Error retrieving scheduled talks"));
        }
    }

    /// <summary>
    /// Get a scheduled talk assignment by ID
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <returns>Scheduled talk details with progress</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ScheduledTalkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            // Use GetScheduledTalksQuery with a filter to get by ID
            // This ensures proper tenant isolation
            var query = new GetScheduledTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                PageNumber = 1,
                PageSize = 1
            };

            var result = await _mediator.Send(query);
            var scheduledTalk = result.Items.FirstOrDefault(x => x.Id == id);

            if (scheduledTalk == null)
            {
                return NotFound(new { message = "Scheduled talk not found" });
            }

            // Return as ScheduledTalkDto (the list DTO contains the necessary fields)
            return Ok(scheduledTalk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scheduled talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error retrieving scheduled talk" });
        }
    }

    /// <summary>
    /// Get scheduled talks for a specific employee
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <param name="status">Optional filter by status</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of scheduled talks for the employee</returns>
    [HttpGet("by-employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(PaginatedList<ScheduledTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByEmployee(
        Guid employeeId,
        [FromQuery] ScheduledTalkStatus? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = new GetScheduledTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                EmployeeId = employeeId,
                Status = status,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving scheduled talks for employee {EmployeeId}", employeeId);
            return StatusCode(500, Result.Fail("Error retrieving scheduled talks"));
        }
    }

    /// <summary>
    /// Get overdue scheduled talks
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of overdue scheduled talks</returns>
    [HttpGet("overdue")]
    [ProducesResponseType(typeof(PaginatedList<ScheduledTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverdue(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = new GetScheduledTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                Status = ScheduledTalkStatus.Overdue,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving overdue scheduled talks");
            return StatusCode(500, Result.Fail("Error retrieving overdue scheduled talks"));
        }
    }

    /// <summary>
    /// Get pending scheduled talks (not started)
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of pending scheduled talks</returns>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(PaginatedList<ScheduledTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = new GetScheduledTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                Status = ScheduledTalkStatus.Pending,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending scheduled talks");
            return StatusCode(500, Result.Fail("Error retrieving pending scheduled talks"));
        }
    }

    /// <summary>
    /// Get in-progress scheduled talks
    /// </summary>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of in-progress scheduled talks</returns>
    [HttpGet("in-progress")]
    [ProducesResponseType(typeof(PaginatedList<ScheduledTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInProgress(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = new GetScheduledTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                Status = ScheduledTalkStatus.InProgress,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving in-progress scheduled talks");
            return StatusCode(500, Result.Fail("Error retrieving in-progress scheduled talks"));
        }
    }

    /// <summary>
    /// Get completed scheduled talks
    /// </summary>
    /// <param name="fromDate">Optional filter from date</param>
    /// <param name="toDate">Optional filter to date</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of completed scheduled talks</returns>
    [HttpGet("completed")]
    [ProducesResponseType(typeof(PaginatedList<ScheduledTalkListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCompleted(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = new GetScheduledTalksQuery
            {
                TenantId = _currentUserService.TenantId,
                Status = ScheduledTalkStatus.Completed,
                DueDateFrom = fromDate,
                DueDateTo = toDate,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving completed scheduled talks");
            return StatusCode(500, Result.Fail("Error retrieving completed scheduled talks"));
        }
    }

    /// <summary>
    /// Send a reminder for a scheduled talk assignment
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <returns>Success status</returns>
    [HttpPost("{id:guid}/reminder")]
    [Authorize(Policy = "ToolboxTalks.Schedule")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendReminder(Guid id)
    {
        try
        {
            var command = new SendScheduledTalkReminderCommand
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            await _mediator.Send(command);
            return Ok(Result.Ok("Reminder sent successfully"));
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
            _logger.LogError(ex, "Error sending reminder for scheduled talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error sending reminder" });
        }
    }

    /// <summary>
    /// Cancel a scheduled talk assignment
    /// </summary>
    /// <param name="id">Scheduled talk ID</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ToolboxTalks.Schedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            var command = new CancelScheduledTalkCommand
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
            _logger.LogError(ex, "Error cancelling scheduled talk {ScheduledTalkId}", id);
            return StatusCode(500, new { message = "Error cancelling assignment" });
        }
    }
}
