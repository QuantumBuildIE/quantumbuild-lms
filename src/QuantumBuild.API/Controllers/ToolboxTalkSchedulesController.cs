using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.CancelToolboxTalkSchedule;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.CreateToolboxTalkSchedule;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.ProcessToolboxTalkSchedule;
using QuantumBuild.Modules.ToolboxTalks.Application.Commands.UpdateToolboxTalkSchedule;
using QuantumBuild.Modules.ToolboxTalks.Application.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkScheduleById;
using QuantumBuild.Modules.ToolboxTalks.Application.Queries.GetToolboxTalkSchedules;
using QuantumBuild.Modules.ToolboxTalks.Domain.Enums;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for managing toolbox talk schedules (bulk assignment configuration)
/// </summary>
[ApiController]
[Route("api/toolbox-talks/schedules")]
[Authorize(Policy = "ToolboxTalks.View")]
public class ToolboxTalkSchedulesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ToolboxTalkSchedulesController> _logger;

    public ToolboxTalkSchedulesController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        ILogger<ToolboxTalkSchedulesController> logger)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get all toolbox talk schedules with pagination and filtering
    /// </summary>
    /// <param name="toolboxTalkId">Optional filter by toolbox talk</param>
    /// <param name="status">Optional filter by status</param>
    /// <param name="dateFrom">Optional filter from date</param>
    /// <param name="dateTo">Optional filter to date</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of items per page</param>
    /// <returns>Paginated list of schedules</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ToolboxTalkScheduleListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? toolboxTalkId = null,
        [FromQuery] ToolboxTalkScheduleStatus? status = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        try
        {
            var query = new GetToolboxTalkSchedulesQuery
            {
                TenantId = _currentUserService.TenantId,
                ToolboxTalkId = toolboxTalkId,
                Status = status,
                DateFrom = dateFrom,
                DateTo = dateTo,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving toolbox talk schedules");
            return StatusCode(500, Result.Fail("Error retrieving toolbox talk schedules"));
        }
    }

    /// <summary>
    /// Get a toolbox talk schedule by ID
    /// </summary>
    /// <param name="id">Schedule ID</param>
    /// <returns>Schedule details with assignments</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ToolboxTalkScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var query = new GetToolboxTalkScheduleByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            var result = await _mediator.Send(query);
            if (result == null)
            {
                return NotFound(new { message = "Schedule not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving schedule {ScheduleId}", id);
            return StatusCode(500, new { message = "Error retrieving schedule" });
        }
    }

    /// <summary>
    /// Create a new toolbox talk schedule
    /// </summary>
    /// <param name="command">Schedule creation data</param>
    /// <returns>Created schedule</returns>
    [HttpPost]
    [Authorize(Policy = "ToolboxTalks.Schedule")]
    [ProducesResponseType(typeof(ToolboxTalkScheduleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateToolboxTalkScheduleCommand command)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray()
                );
            _logger.LogWarning("Model state validation failed for CreateToolboxTalkScheduleCommand: {@Errors}", errors);
            return BadRequest(new { message = "Validation failed", errors });
        }

        try
        {
            var commandWithTenant = command with { TenantId = _currentUserService.TenantId };
            var result = await _mediator.Send(commandWithTenant);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation creating schedule");
            return BadRequest(new { message = ex.Message });
        }
        catch (FluentValidation.ValidationException ex)
        {
            _logger.LogWarning("FluentValidation failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message, errors = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schedule");
            return StatusCode(500, new { message = "Error creating schedule" });
        }
    }

    /// <summary>
    /// Update an existing toolbox talk schedule
    /// </summary>
    /// <param name="id">Schedule ID</param>
    /// <param name="command">Updated schedule data</param>
    /// <returns>Updated schedule</returns>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ToolboxTalks.Schedule")]
    [ProducesResponseType(typeof(ToolboxTalkScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateToolboxTalkScheduleCommand command)
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
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (FluentValidation.ValidationException ex)
        {
            return BadRequest(new { message = ex.Message, errors = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule {ScheduleId}", id);
            return StatusCode(500, new { message = "Error updating schedule" });
        }
    }

    /// <summary>
    /// Cancel a toolbox talk schedule
    /// </summary>
    /// <param name="id">Schedule ID</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ToolboxTalks.Schedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        try
        {
            var command = new CancelToolboxTalkScheduleCommand
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            await _mediator.Send(command);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling schedule {ScheduleId}", id);
            return StatusCode(500, new { message = "Error cancelling schedule" });
        }
    }

    /// <summary>
    /// Process a schedule to create individual scheduled talk assignments
    /// </summary>
    /// <param name="id">Schedule ID</param>
    /// <returns>Processing result with assignments created</returns>
    [HttpPost("{id:guid}/process")]
    [Authorize(Policy = "ToolboxTalks.Schedule")]
    [ProducesResponseType(typeof(ProcessScheduleResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Process(Guid id)
    {
        try
        {
            var command = new ProcessToolboxTalkScheduleCommand
            {
                TenantId = _currentUserService.TenantId,
                ScheduleId = id
            };

            var result = await _mediator.Send(command);
            return Ok(new ProcessScheduleResultDto
            {
                ScheduleId = id,
                TalksCreated = result.TalksCreated,
                ScheduleCompleted = result.ScheduleCompleted,
                NextRunDate = result.NextRunDate,
                Message = result.ScheduleCompleted
                    ? $"Schedule completed. Created {result.TalksCreated} assignment(s)."
                    : $"Created {result.TalksCreated} assignment(s). Next run: {result.NextRunDate:yyyy-MM-dd}"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing schedule {ScheduleId}", id);
            return StatusCode(500, new { message = "Error processing schedule" });
        }
    }
}

/// <summary>
/// Result of processing a schedule
/// </summary>
public record ProcessScheduleResultDto
{
    public Guid ScheduleId { get; init; }
    public int TalksCreated { get; init; }
    public bool ScheduleCompleted { get; init; }
    public DateTime? NextRunDate { get; init; }
    public string Message { get; init; } = string.Empty;
}
