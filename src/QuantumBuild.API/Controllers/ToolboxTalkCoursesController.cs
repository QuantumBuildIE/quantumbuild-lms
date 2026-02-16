using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuantumBuild.Core.Application.Interfaces;
using QuantumBuild.Core.Application.Models;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Commands;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.DTOs;
using QuantumBuild.Modules.ToolboxTalks.Application.Features.Courses.Queries;

namespace QuantumBuild.API.Controllers;

/// <summary>
/// Controller for managing toolbox talk courses
/// </summary>
[ApiController]
[Route("api/toolbox-talks/courses")]
[Authorize(Policy = "ToolboxTalks.View")]
public class ToolboxTalkCoursesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ToolboxTalkCoursesController> _logger;

    public ToolboxTalkCoursesController(
        IMediator mediator,
        ICurrentUserService currentUserService,
        ILogger<ToolboxTalkCoursesController> logger)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get all courses for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(Result<List<ToolboxTalkCourseListDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null)
    {
        try
        {
            var query = new GetToolboxTalkCoursesQuery
            {
                TenantId = _currentUserService.TenantId,
                SearchTerm = searchTerm,
                IsActive = isActive
            };

            var result = await _mediator.Send(query);
            return Ok(Result.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving toolbox talk courses");
            return StatusCode(500, Result.Fail("Error retrieving toolbox talk courses"));
        }
    }

    /// <summary>
    /// Get a course by ID with items and translations
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ToolboxTalkCourseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var query = new GetToolboxTalkCourseByIdQuery
            {
                TenantId = _currentUserService.TenantId,
                Id = id
            };

            var result = await _mediator.Send(query);
            if (result == null)
            {
                return NotFound(new { message = "Course not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving course {CourseId}", id);
            return StatusCode(500, new { message = "Error retrieving course" });
        }
    }

    /// <summary>
    /// Create a new course
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "ToolboxTalks.Create")]
    [ProducesResponseType(typeof(ToolboxTalkCourseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateToolboxTalkCourseDto dto)
    {
        try
        {
            var command = new CreateToolboxTalkCourseCommand
            {
                TenantId = _currentUserService.TenantId,
                Dto = dto
            };

            var result = await _mediator.Send(command);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating course");
            return StatusCode(500, new { message = "Error creating course" });
        }
    }

    /// <summary>
    /// Update an existing course
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ToolboxTalks.Edit")]
    [ProducesResponseType(typeof(ToolboxTalkCourseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateToolboxTalkCourseDto dto)
    {
        try
        {
            var command = new UpdateToolboxTalkCourseCommand
            {
                Id = id,
                TenantId = _currentUserService.TenantId,
                Dto = dto
            };

            var result = await _mediator.Send(command);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating course {CourseId}", id);
            return StatusCode(500, new { message = "Error updating course" });
        }
    }

    /// <summary>
    /// Delete a course (soft delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ToolboxTalks.Delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var command = new DeleteToolboxTalkCourseCommand
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
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting course {CourseId}", id);
            return StatusCode(500, new { message = "Error deleting course" });
        }
    }

    /// <summary>
    /// Add a talk to a course
    /// </summary>
    [HttpPost("{id:guid}/items")]
    [Authorize(Policy = "ToolboxTalks.Edit")]
    [ProducesResponseType(typeof(ToolboxTalkCourseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddItem(Guid id, [FromBody] CreateToolboxTalkCourseItemDto dto)
    {
        try
        {
            var command = new AddCourseItemCommand
            {
                CourseId = id,
                TenantId = _currentUserService.TenantId,
                Dto = dto
            };

            var result = await _mediator.Send(command);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding item to course {CourseId}", id);
            return StatusCode(500, new { message = "Error adding item to course" });
        }
    }

    /// <summary>
    /// Remove a talk from a course
    /// </summary>
    [HttpDelete("{id:guid}/items/{talkId:guid}")]
    [Authorize(Policy = "ToolboxTalks.Edit")]
    [ProducesResponseType(typeof(ToolboxTalkCourseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveItem(Guid id, Guid talkId)
    {
        try
        {
            var command = new RemoveCourseItemCommand
            {
                CourseId = id,
                ToolboxTalkId = talkId,
                TenantId = _currentUserService.TenantId
            };

            var result = await _mediator.Send(command);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing item from course {CourseId}", id);
            return StatusCode(500, new { message = "Error removing item from course" });
        }
    }

    /// <summary>
    /// Reorder/bulk update course items
    /// </summary>
    [HttpPut("{id:guid}/items")]
    [Authorize(Policy = "ToolboxTalks.Edit")]
    [ProducesResponseType(typeof(ToolboxTalkCourseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateItems(Guid id, [FromBody] UpdateCourseItemsDto dto)
    {
        try
        {
            var command = new UpdateCourseItemsCommand
            {
                CourseId = id,
                TenantId = _currentUserService.TenantId,
                Dto = dto
            };

            var result = await _mediator.Send(command);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating items for course {CourseId}", id);
            return StatusCode(500, new { message = "Error updating course items" });
        }
    }
}
